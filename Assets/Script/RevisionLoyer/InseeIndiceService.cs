using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using UnityEngine.Networking;

public static class InseeIndiceService
{
    private static readonly Dictionary<IndiceImmo, (string code, string label)> Indices =
        new Dictionary<IndiceImmo, (string, string)>
        {
            { IndiceImmo.ILC,  ("001532540", "Indice des Loyers Commerciaux") },
            { IndiceImmo.IRL,  ("001515334", "Indice de Référence des Loyers") },
            { IndiceImmo.ILAT, ("001617113", "Indice Loyers Activités Tertiaires") },
        };

    public static string GetLabel(IndiceImmo type) => Indices[type].label;

    /// Enumerator pur : tourne sur la coroutine de l'appelant
    public static IEnumerator FetchObservations(IndiceImmo type,
        Action<List<(string periode, float valeur)>> onSuccess, Action<string> onError)
    {
        string url = $"https://www.bdm.insee.fr/series/sdmx/data/SERIES_BDM/{Indices[type].code}";

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(req.error);
                yield break;
            }

            var obs = ParseObservations(req.downloadHandler.text);
            if (obs.Count == 0) onError?.Invoke("Aucune donnée reçue");
            else onSuccess?.Invoke(obs);
        }
    }

    /// Recherche avec fallback (même trimestre, jusqu'à 5 ans en arrière).
    /// Utilisé UNIQUEMENT pour l'indice de départ à l'initialisation.
    public static (string periode, float valeur) TrouveAvecFallback(
        List<(string periode, float valeur)> observations, string periodeDepart)
    {
        var obs = observations.Find(o => Normalize(o.periode) == Normalize(periodeDepart));
        if (!string.IsNullOrEmpty(obs.periode)) return obs;

        string trimestre = Normalize(periodeDepart).Split('-')[1];
        int annee = int.Parse(Normalize(periodeDepart).Substring(0, 4));

        for (int recul = 1; recul <= 5; recul++)
        {
            obs = observations.Find(o => Normalize(o.periode) == $"{annee - recul}-{trimestre}");
            if (!string.IsNullOrEmpty(obs.periode)) return obs;
        }
        return ("", 0f);
    }

    /// Recherche stricte, sans fallback. Utilisé pour l'indice de révision.
    public static (string periode, float valeur) TrouveExact(
        List<(string periode, float valeur)> observations, string periode)
    {
        return observations.Find(o => Normalize(o.periode) == Normalize(periode));
    }

    public static string Normalize(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        input = input.Trim().ToUpper().Replace("Q", "T").Replace(" ", "");
        if (input.Length == 6 && input[4] == 'T')
            input = input.Insert(4, "-");
        return input;
    }

    private static List<(string periode, float valeur)> ParseObservations(string xml)
    {
        var result = new List<(string, float)>();
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        foreach (XmlNode obs in doc.SelectNodes("//*[local-name()='Obs']"))
        {
            string periode = obs.Attributes["TIME_PERIOD"]?.Value ?? "";
            string valStr = obs.Attributes["OBS_VALUE"]?.Value ?? "";
            if (string.IsNullOrEmpty(periode) || string.IsNullOrEmpty(valStr)) continue;
            if (float.TryParse(valStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                result.Add((periode, v));
        }
        return result;
    }
}
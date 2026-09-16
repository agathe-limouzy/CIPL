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

        // Décomposition sûre : une période illisible (ancien format « T1 », « 20261 »)
        // faisait lever IndexOutOfRange / FormatException au lieu de renvoyer « rien ».
        if (!TryDecompose(periodeDepart, out int annee, out int trim)) return ("", 0f);
        string trimestre = $"T{trim}";

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

    /// Observation la plus récente réellement publiée (tri année puis trimestre).
    /// Sert à suggérer un trimestre valide quand le trimestre voulu n'est pas publié.
    public static (string periode, float valeur) DernierPublie(
        List<(string periode, float valeur)> observations)
    {
        (string periode, float valeur) best = ("", 0f);
        int bestRang = int.MinValue;
        if (observations == null) return best;
        foreach (var o in observations)
        {
            if (!TryRang(o.periode, out int rang)) continue;
            if (rang > bestRang) { bestRang = rang; best = o; }
        }
        return best;
    }

    /// "YYYY-TQ" → (année, trimestre). false si la chaîne est illisible.
    /// À utiliser PARTOUT plutôt que `Normalize(x).Split('-')[1]` : Normalize n'insère
    /// le tiret que sur une chaîne de 6 caractères, donc une valeur ancienne comme
    /// "T1" ou "20261" faisait lever IndexOutOfRange.
    public static bool TryDecompose(string periode, out int annee, out int trim)
    {
        annee = 0; trim = 0;
        string n = Normalize(periode);                       // "2026-T4"
        if (n.Length < 7 || n[5] != 'T') return false;
        if (!int.TryParse(n.Substring(0, 4), out annee)) return false;
        if (!int.TryParse(n.Substring(6, 1), out trim)) return false;
        return trim >= 1 && trim <= 4;
    }

    /// "YYYY-TQ" → rang comparable (année*4 + trimestre). false si illisible.
    private static bool TryRang(string periode, out int rang)
    {
        rang = 0;
        if (!TryDecompose(periode, out int annee, out int trim)) return false;
        rang = annee * 4 + trim;
        return true;
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
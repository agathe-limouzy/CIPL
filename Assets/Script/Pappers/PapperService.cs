using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class PapperService : MonoBehaviour
{

    private const string ApiBaseUrl ="https://recherche-entreprises.api.gouv.fr/search";

public void FetchBySiret(string siret,
Action<AnnuaireEntreprise> onSuccess, Action<string> onError)
{
    StartCoroutine(Interroger(siret, onSuccess, onError));
}

/// La requête elle-même, sans dépendre d'une fiche ouverte : la mise à jour au
/// lancement (PappersSync) parcourt des locataires dont aucun écran n'existe.
/// À lancer depuis n'importe quel MonoBehaviour.
public static IEnumerator Interroger(string siret,
Action<AnnuaireEntreprise> onSuccess, Action<string> onError)
{
    string url =$"{ApiBaseUrl}?q={siret.Trim()}&page=1&per_page=1";
    using var req = UnityWebRequest.Get(url);
    yield return req.SendWebRequest();

    if (req.result != UnityWebRequest.Result.Success)
    {
        onError?.Invoke(req.error);
        yield break;
    }

    var response = JsonUtility.FromJson<AnnuaireResponse>(req.downloadHandler.text);

    if (response.results == null || response.results.Length== 0)
    {
        onError?.Invoke("Entreprise non trouvée pour ce SIRET");

        yield break;
    }

    onSuccess?.Invoke(response.results[0]);
}

/// Étape 10 : la même interrogation, attendable (« Ajouter une entreprise »). `introuvable` :
/// l'annuaire a répondu, sans résultat ; une erreur sans `introuvable` : il n'a pas répondu.
public static async System.Threading.Tasks.Task<(AnnuaireEntreprise data, string erreur, bool introuvable)> Chercher(string siret)
{
    using var req = UnityWebRequest.Get($"{ApiBaseUrl}?q={UnityWebRequest.EscapeURL((siret ?? "").Trim())}&page=1&per_page=1");
    var fin = new System.Threading.Tasks.TaskCompletionSource<bool>();
    req.SendWebRequest().completed += _ => fin.TrySetResult(true);
    await fin.Task;
    if (req.result != UnityWebRequest.Result.Success) return (null, req.error, false);
    AnnuaireResponse response;
    try { response = JsonUtility.FromJson<AnnuaireResponse>(req.downloadHandler.text); }
    catch (Exception e) { return (null, "réponse illisible : " + e.Message, false); }
    return response?.results == null || response.results.Length == 0
        ? (null, "Entreprise non trouvée pour ce SIRET", true)
        : (response.results[0], null, false);
}
  }

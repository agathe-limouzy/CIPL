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
  }

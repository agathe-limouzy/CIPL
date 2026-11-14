using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PrefabBatLoc : MonoBehaviour
{

    // button Action
    public Button save;
    public Button modifyBatiment;
    public Button Delete;

    /// Vrai si la fiche est en cours de modification, c'est-à-dire si elle porte des
    /// saisies non encore reversées dans les données.
    /// L'information n'est pas dupliquée dans un booléen (qui se désynchroniserait tôt
    /// ou tard) : elle est lue là où elle vit déjà — Modify() affiche le bouton
    /// Enregistrer, SaveBatiment() le masque.
    public bool EnEdition => save != null && save.gameObject.activeSelf;

    public  virtual void InitializeBatiment(Batiment newBatiment, bool NeedToModify)
    {

    }

    public virtual void InitializeLocataire(Locataire newLocataire, bool NeedToModify)
    {

    }

    public  virtual void Modify()
    {

    }

    public   void SaveCorrectlyFloat(ref float target, string origin)
    {
        if (!string.IsNullOrEmpty(origin))
        {
            if (SaisieNumerique.TryParse(origin, out float result))
            {
                target = result;
            }
            else
            {
                Debug.LogWarning($"[Save] '{origin}' n'est pas un nombre valide");
                target = 0f;
            }
        }
        else
        {
            Debug.Log("[Save] Champ vide → 0");
            target = 0f;
        }
    }


    public void SaveCorrectlyInt(ref int target, string origin)
    {
        if (!string.IsNullOrEmpty(origin))
        {
            if (int.TryParse(origin,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out int result))
            {
                target = result;
            }
            else
            {
                Debug.LogWarning($"[Save] '{origin}' n'est pas un nombre valide");
                target = 0;
            }
        }
        else
        {
            Debug.Log("[Save] Champ vide → 0");
            target = 0;
        }
    }


    /// Revue 17 (D7a-14, lot D) : le refus à afficher au centre pour un nombre illisible (« 2B », « 120 m2 »), ou null s'il se lit
    /// (le vide aussi : il vaut 0). Les fiches le demandent AVANT toute écriture : SaveCorrectlyFloat / SaveCorrectlyInt
    /// enregistraient 0 sans rien dire. `entier` : la même lecture que SaveCorrectlyInt (numéro du lot).
    public static string RefusNombre(string texte, string quoi, bool entier = false)
    {
        if (string.IsNullOrWhiteSpace(texte)) return null;
        bool lu = entier
            ? int.TryParse(texte, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out int _)
            : SaisieNumerique.TryParse(texte, out float _);
        return lu ? null : $"« {texte.Trim()} » n'est pas un nombre ({quoi}). Corrigez-le ou videz le champ.";
    }

    public virtual void SaveBatiment()
    {
    }

    public virtual string getName()
    {
        return null;
    }
    public virtual string getID()
    {
        return null;
    }
}

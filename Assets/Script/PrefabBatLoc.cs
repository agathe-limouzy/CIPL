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

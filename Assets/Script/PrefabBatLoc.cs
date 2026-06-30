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
            if (float.TryParse(origin,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float result))
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

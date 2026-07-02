using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum TypeInvestissement { Achat, Travaux }

public class InvestissementListPanel : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text txtTitre;
    public Transform listContent;
    public GameObject achatItemPrefab;
    public GameObject travauxItemPrefab;
    public Button btnAjouter;
    public Button btnFermer;

    [Header("Form panels")]
    public AchatFormPanel achatFormPanel;
    public TravauxFormPanel travauxFormPanel;

    private BatimentPrefab _batiment;
    private TypeInvestissement _type;
    private Action _onChanged;

    public void Open(BatimentPrefab bp, TypeInvestissement type, Action onChanged)
    {
        _batiment = bp;
        _type = type;
        _onChanged = onChanged;

        txtTitre.text = type == TypeInvestissement.Achat
            ? "Historique des achats"
            : "Historique des travaux";

        gameObject.SetActive(true);
        Rebuild();

        btnAjouter.onClick.RemoveAllListeners();
        btnAjouter.onClick.AddListener(OpenAddForm);

        btnFermer.onClick.RemoveAllListeners();
        btnFermer.onClick.AddListener(() => gameObject.SetActive(false));
    }

    private void Rebuild()
    {
        foreach (Transform child in listContent) Destroy(child.gameObject);

        if (_type == TypeInvestissement.Achat)
        {
            foreach (var achat in _batiment.getBatiment().historiquesAchat)
            {
                var go = Instantiate(achatItemPrefab, listContent);
                go.GetComponent<AchatItemUI>().Setup(achat,
                    () => achatFormPanel.Open(achat, OnAchatSaved),
                    () => { _batiment.getBatiment().historiquesAchat.RemoveAll(a => a.id == achat.id); SaveAndRefresh(); });
            }
        }
        else
        {
            foreach (var travaux in _batiment.getBatiment().travaux)
            {
                var go = Instantiate(travauxItemPrefab, listContent);
                go.GetComponent<TravauxItemUI>().Setup(travaux,
                    () => travauxFormPanel.Open(travaux, OnTravauxSaved),
                    () => { _batiment.getBatiment().travaux.RemoveAll(t => t.id == travaux.id); SaveAndRefresh(); });
            }
        }

        // ← Forcer le rebuild après instantiation
        StartCoroutine(RebuildLayout());
    }

    private System.Collections.IEnumerator RebuildLayout()
    {
        yield return null;
        yield return null;
        Canvas.ForceUpdateCanvases();
        var rt = listContent as RectTransform;
        while (rt != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            rt = rt.parent as RectTransform;
        }
    }

    private void OpenAddForm()
    {
        if (_type == TypeInvestissement.Achat)
            achatFormPanel.Open(null, OnAchatSaved);
        else
            travauxFormPanel.Open(null, OnTravauxSaved);
    }

    private void OnAchatSaved(AchatFinancement data)
    {
        var list = _batiment.getBatiment().historiquesAchat;
        int idx = list.FindIndex(a => a.id == data.id);
        if (idx >= 0) list[idx] = data; else list.Add(data);
        SaveAndRefresh();
    }

    private void OnTravauxSaved(TravauxFinancement data)
    {
        var list = _batiment.getBatiment().travaux;
        int idx = list.FindIndex(t => t.id == data.id);
        if (idx >= 0) list[idx] = data; else list.Add(data);
        SaveAndRefresh();
    }

    private void SaveAndRefresh()
    {
        BatimentManager.Instance.SaveBatiment(_batiment.getBatiment());
        _onChanged?.Invoke();
        Rebuild();
    }
}
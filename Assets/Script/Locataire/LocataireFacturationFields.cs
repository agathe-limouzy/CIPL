using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Ajoute (au runtime) des champs de facturation à la fiche locataire, en clonant
/// des champs InputAndText existants (rendu natif). Piloté par LocatairePrefab.
///
/// General : RIB du locataire (Titulaire / IBAN / BIC).
/// Autre   : Date de révision du dépôt + bouton « Révision dépôt de garantie » (pop-up).
/// (Les paramètres LOYER — jour de demande, mois facturés, régularisation — sont
///  dans le pop-up « Révision du loyer » : voir RevisionPanel.)
public class LocataireFacturationFields : MonoBehaviour
{
    InputAndText ribTitulaire, ribIban, ribBic;
    InputAndText dateRevisionDepot;

    LocatairePrefab _fiche;
    bool _built;

    public void EnsureBuilt(LocatairePrefab fiche)
    {
        if (_built) return;
        _built = true;
        _fiche = fiche;

        var src = fiche.emailLocataireTxt;   // champ modèle à cloner
        if (src == null) return;

        // ── General : RIB du locataire ──────────────────────────────────────
        var generalContent = src.transform.parent;
        ribTitulaire = Clone(src, generalContent, "Titulaire (RIB locataire)", "Nom du titulaire", "");
        ribIban      = Clone(src, generalContent, "IBAN locataire", "FR76 …", "");
        ribBic       = Clone(src, generalContent, "BIC locataire", "BNPAFRPP", "");

        // ── Dépôt : dans la carte « Autre » ─────────────────────────────────
        Transform autreContent = fiche.depotDeGarantieTxt != null
            ? fiche.depotDeGarantieTxt.transform.parent.parent
            : null;
        if (autreContent != null)
        {
            dateRevisionDepot = Clone(src, autreContent, "Date de révision du dépôt", "JJ / MM / AAAA", "");
            var btn = UIFactory.Button(autreContent, "Révision dépôt de garantie", UITheme.Primaire, Color.white, 40, 16);
            btn.onClick.AddListener(OpenRevisionPopup);
        }
    }

    InputAndText Clone(InputAndText src, Transform parent, string label, string placeholder, string unit)
    {
        var go = Instantiate(src.gameObject, parent);
        go.name = label;
        var iat = go.GetComponent<InputAndText>();

        var titre = go.transform.Find("title")?.GetComponent<TMP_Text>();
        if (titre != null) titre.text = label;

        var q = go.transform.Find("quantité")?.GetComponent<TMP_Text>();
        if (q != null) q.text = unit;

        if (iat.inputModify != null && iat.inputModify.placeholder != null)
        {
            var ph = iat.inputModify.placeholder.GetComponent<TMP_Text>();
            if (ph != null) ph.text = placeholder;
        }
        iat.ApplyValue("");
        return iat;
    }

    // ── Révision du dépôt de garantie (pop-up) ──────────────────────────────

    void OpenRevisionPopup()
    {
        if (_fiche == null) return;
        var loc = _fiche.GetLocataire();
        if (loc == null) return;
        float monthlyHT = loc.loyerAnnuel / 12f;

        var canvas = _fiche.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        var scrim = UIFactory.Rect("RevScrim", canvas.rootCanvas.transform);
        UIFactory.Stretch(scrim);
        scrim.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f);
        scrim.SetAsLastSibling();

        var cardImg = UIFactory.Panel("RevCard", scrim, UITheme.Carte);
        UIFactory.Border(cardImg.gameObject);
        var card = (RectTransform)cardImg.transform;
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
        card.sizeDelta = new Vector2(540, 100);
        var v = cardImg.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 10; v.padding = new RectOffset(18, 18, 16, 16);
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        var csf = cardImg.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        UIFactory.Text(v.transform, "Révision du dépôt de garantie", 22, UITheme.TextePrincipal, true);

        UIFactory.Text(v.transform, "Date de révision", 15, UITheme.TexteSecondaire);
        var dateInput = UIFactory.Input(v.transform, "JJ / MM / AAAA");
        dateInput.text = DateTime.Today.ToString("dd/MM/yyyy");

        var ttcToggle = UIFactory.Toggle(v.transform, "Sur le loyer TTC (sinon HT)", false);

        UIFactory.Text(v.transform, "Loyer mensuel (modifiable)", 15, UITheme.TexteSecondaire);
        var loyerInput = UIFactory.Input(v.transform, "0");
        loyerInput.contentType = TMP_InputField.ContentType.DecimalNumber;

        UIFactory.Text(v.transform, "Nombre de mois de loyer", 15, UITheme.TexteSecondaire);
        var moisInput = UIFactory.Input(v.transform, "ex. 3");
        moisInput.contentType = TMP_InputField.ContentType.IntegerNumber;

        var resultLine = UIFactory.Text(v.transform, "", 18, UITheme.Primaire, true);

        Action refreshLoyer = () =>
        {
            float m = ttcToggle.isOn ? monthlyHT * 1.2f : monthlyHT;
            loyerInput.text = m.ToString("0.00");
        };
        ttcToggle.onValueChanged.AddListener(_ => refreshLoyer());
        refreshLoyer();

        float nouveau = 0f; bool hasComputed = false;

        var actions = UIFactory.HBox(v.transform, 10);
        UIFactory.LE(actions.gameObject, minH: 46);

        var cancel = UIFactory.Button(actions.transform, "Fermer", UITheme.Carte, UITheme.TextePrincipal, 44, 18);
        UIFactory.Border(cancel.gameObject); UIFactory.LE(cancel.gameObject, flexW: 1);
        cancel.onClick.AddListener(() => Destroy(scrim.gameObject));

        var reviser = UIFactory.Button(actions.transform, "Réviser", UITheme.Carte, UITheme.Primaire, 44, 18);
        UIFactory.Border(reviser.gameObject); UIFactory.LE(reviser.gameObject, flexW: 1);
        reviser.onClick.AddListener(() =>
        {
            int.TryParse(moisInput.text, out int nb);
            if (nb <= 0) { resultLine.text = "Indique un nombre de mois valide."; hasComputed = false; return; }
            float.TryParse(loyerInput.text, out float m);
            nouveau = m * nb;
            hasComputed = true;
            resultLine.text = $"Ancien : {loc.depotDeGarantie:0.00} €   ⇄   Nouveau : {nouveau:0.00} €";
        });

        var apply = UIFactory.Button(actions.transform, "Appliquer", UITheme.Primaire, Color.white, 44, 18);
        UIFactory.LE(apply.gameObject, flexW: 1);
        apply.onClick.AddListener(() =>
        {
            if (!hasComputed) { resultLine.text = "Clique d'abord sur « Réviser »."; return; }
            loc.depotDeGarantie = nouveau;
            loc.dateRevisionDepotISO = DateTime.TryParse(dateInput.text, out var dt)
                ? dt.ToString("yyyy-MM-dd") : dateInput.text;
            _fiche.depotDeGarantieTxt.ApplyValue(nouveau.ToString("0.00"));
            dateRevisionDepot?.ApplySave(loc.dateRevisionDepotISO);
            _fiche.batimentPrefabOrigin.SaveAfterModifyToDoListLocataire();
            UndoToast.Instance?.ShowInfo($"Dépôt révisé : {nouveau:0.00} €");
            Destroy(scrim.gameObject);
        });
    }

    // ── Load / Modify / Save ────────────────────────────────────────────────

    public void Load(Locataire loc)
    {
        ribTitulaire?.ApplySave(loc.ribLocataireTitulaire ?? "");
        ribIban?.ApplySave(loc.ribLocataireIban ?? "");
        ribBic?.ApplySave(loc.ribLocataireBic ?? "");
        dateRevisionDepot?.ApplySave(loc.dateRevisionDepotISO ?? "");
    }

    public void Modify()
    {
        ribTitulaire?.Modify(); ribIban?.Modify(); ribBic?.Modify();
        dateRevisionDepot?.Modify();
    }

    public void Save(Locataire loc)
    {
        if (ribTitulaire != null) loc.ribLocataireTitulaire = ribTitulaire.GetNewSave();
        if (ribIban != null) loc.ribLocataireIban = ribIban.GetNewSave();
        if (ribBic != null) loc.ribLocataireBic = ribBic.GetNewSave();
        if (dateRevisionDepot != null) loc.dateRevisionDepotISO = dateRevisionDepot.GetNewSave();
    }
}

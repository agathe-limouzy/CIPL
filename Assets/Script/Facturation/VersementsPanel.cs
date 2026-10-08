using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Fenêtre « Paiement » d'une facture (paiement partiel, maquette validée le 08/10) : les
/// versements reçus, datés, avec « Retirer » ; payé et reste à payer ; ajout d'un
/// versement. Tout réglé → la facture passe « Payé ». Les règles sont dans FacturationSuivi.
public static class VersementsPanel
{
    static string Euros(float v) => v.ToString("#,##0.00", FacturePdfService.FrCulture) + " €";

    public static void Ouvrir(LocatairePrefab fiche, FactureEtat ligne, Action apres)
    {
        var loc = fiche != null ? fiche.GetLocataire() : null;
        var canvas = fiche != null ? fiche.GetComponentInParent<Canvas>() : null;
        var rec = FacturationSuivi.Porteuse(loc, ligne?.key);
        if (loc == null || canvas == null || rec == null) return;
        FacturationSuivi.Solde(loc, ligne.key, out float total, out float payeAvant);
        // Copie : « Annuler » ne touche à rien.
        var liste = (rec.versements ?? new List<Versement>())
            .Select(x => new Versement { dateISO = x.dateISO, montant = x.montant }).ToList();

        var scrim = UIFactory.Rect("VersementsScrim", canvas.rootCanvas.transform);
        UIFactory.Stretch(scrim);
        scrim.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f);
        scrim.SetAsLastSibling();
        Action fermer = () => UnityEngine.Object.Destroy(scrim.gameObject);

        var cardImg = UIFactory.Panel("VersementsCard", scrim, UITheme.Carte);
        UIFactory.Border(cardImg.gameObject);
        var card = (RectTransform)cardImg.transform;
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
        card.sizeDelta = new Vector2(580, 100);
        var v = cardImg.gameObject.AddComponent<VerticalLayoutGroup>();
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        cardImg.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        DepartPanel.Entete(v.transform, $"Paiement — {ligne.libelle} · {Euros(total)}", UITheme.Primaire);

        var body = UIFactory.VBox(v.transform, 10, 18, 18, 14, 16, "Body");
        var lignes = UIFactory.VBox(body.transform, 4, name: "Versements").transform;
        var soldes = UIFactory.HBox(body.transform, 16, false, "Soldes");
        var txtPaye = UIFactory.Text(soldes.transform, "", UITheme.Role.Libelle, UITheme.TextePrincipal, true);
        UIFactory.LE(txtPaye.gameObject, flexW: 1);
        var txtReste = UIFactory.Text(soldes.transform, "", UITheme.Role.Libelle, UITheme.Alerte, true);

        UIFactory.Text(body.transform, "Nouveau versement", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        var ajout = UIFactory.HBox(body.transform, 12, false, "Ajout");
        ajout.childAlignment = TextAnchor.LowerLeft;
        var date = DepartPanel.ChampDate(fiche, ajout.transform, "Date", DateTime.Today);
        var montant = DepartPanel.Champ(ajout.transform, "Montant (€)", "", 1f);
        var ajouter = UIFactory.Button(ajout.transform, "Ajouter", UITheme.Carte, UITheme.TextePrincipal, 40, UITheme.Role.Bouton, false);
        UIFactory.Border(ajouter.gameObject);
        UIFactory.LargeurDuTexte(ajouter);

        Action maj = null;
        maj = () =>
        {
            foreach (Transform c in lignes) UnityEngine.Object.Destroy(c.gameObject);
            if (liste.Count == 0)
                UIFactory.Text(lignes, "Aucun versement pour l'instant.", UITheme.Role.Aide, UITheme.TexteSecondaire);
            foreach (var x in liste.OrderBy(x => x.dateISO))
            {
                var vers = x;
                var l = UIFactory.HBox(lignes, 16, false, "Versement");
                UIFactory.Text(l.transform, FacturationSuivi.TryEcheance(x.dateISO, out var d) ? d.ToString("dd/MM/yyyy") : "—",
                    UITheme.Role.Donnee, UITheme.TextePrincipal);
                var m = UIFactory.Text(l.transform, Euros(x.montant), UITheme.Role.Donnee, UITheme.TextePrincipal, true);
                UIFactory.LE(m.gameObject, flexW: 1);
                // Vrai bouton (fond rouge pâle + bordure) : en simple texte, on ne le voyait pas (T6, 08/10).
                var retirer = UIFactory.Button(l.transform, "Retirer", new Color32(0xFC, 0xEB, 0xEB, 0xFF),
                    new Color32(0xA3, 0x2D, 0x2D, 0xFF), 32, UITheme.Role.Action, false);
                UIFactory.Border(retirer.gameObject, new Color32(0xE2, 0x9C, 0x9C, 0xFF));
                UIFactory.LargeurDuTexte(retirer);
                retirer.onClick.AddListener(() => { liste.Remove(vers); maj(); });
            }
            float paye = liste.Sum(x => x.montant), reste = Mathf.Max(0f, total - paye);
            txtPaye.text = "Payé : " + Euros(paye);
            txtReste.text = reste > 0.005f ? "Reste à payer : " + Euros(reste) : "Facture réglée";
            txtReste.color = reste > 0.005f ? UITheme.Alerte : UITheme.Primaire;
            if (montant.placeholder is TMP_Text ph) ph.text = reste > 0.005f ? Euros(reste) : "";
        };
        maj();

        ajouter.onClick.AddListener(() =>
        {
            if (!date.LireDate(out var d)) { ConfirmDialog.Erreur("Date du versement : saisissez une date (JJ/MM/AAAA)."); return; }
            if (!SaisieNumerique.TryParse(montant.text, out float m)) { ConfirmDialog.Erreur($"« {montant.text} » n'est pas un montant."); return; }
            string refus = FacturationSuivi.VerifierVersement(m, total - liste.Sum(x => x.montant));
            if (refus != null) { ConfirmDialog.Erreur(refus); return; }
            liste.Add(new Versement { dateISO = d.ToString("yyyy-MM-dd"), montant = m });
            montant.text = "";
            maj();
        });

        // Pied : Annuler · Enregistrer.
        var pied = UIFactory.HBox(v.transform, 8, false, "Pied");
        pied.padding = new RectOffset(18, 18, 10, 14);
        UIFactory.LE(UIFactory.Rect("Espace", pied.transform).gameObject, flexW: 1);
        var annuler = UIFactory.Button(pied.transform, "Annuler", UITheme.Carte, UITheme.TextePrincipal, 40, UITheme.Role.Bouton, false);
        UIFactory.Border(annuler.gameObject);
        UIFactory.LargeurDuTexte(annuler);
        annuler.onClick.AddListener(() => fermer());
        var enregistrer = UIFactory.Button(pied.transform, "Enregistrer", UITheme.Primaire, Color.white, 40, UITheme.Role.Bouton);
        UIFactory.LargeurDuTexte(enregistrer);
        enregistrer.onClick.AddListener(() =>
        {
            rec.versements = liste.OrderBy(x => x.dateISO).ToList();
            float payeApres = FacturationSuivi.Paye(rec);
            string statut = FacturationSuivi.StatutApresVersements(rec.statut, total, payeAvant, payeApres);
            // SetStatut : toutes les lignes de la facture, et les charges couvertes.
            if (statut != rec.statut)
                FacturationSuivi.SetStatut(loc, ligne, statut,
                    fiche.batimentPrefabOrigin != null ? fiche.batimentPrefabOrigin.getBatiment() : null);
            fiche.batimentPrefabOrigin.SaveAfterModifyToDoListLocataire();
            fermer();
            apres?.Invoke();
            float reste = total - payeApres;
            UndoToast.Instance?.ShowInfo(reste > 0.005f
                ? $"Paiement enregistré : reste {Euros(reste)} à payer."
                : "Facture réglée : elle passe « Payé ».");
        });
    }
}

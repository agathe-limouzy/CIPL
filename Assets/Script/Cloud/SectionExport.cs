using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;

/// Étape 11 : Réglages › Documents › « Exporter toutes les données » (maquette du 23/10). Pour
/// l'admin, et pour tout le monde quand l'abonnement a expiré. Le dernier export (date, bilan,
/// dossier) est retenu sur ce PC.
public class SectionExport
{
    const string Cle = "cipl_export_";   // PlayerPrefs par entreprise : « ticks|dossier|bilan »
    readonly GameObject _section;
    readonly Transform _corps;
    bool _enCours;

    public SectionExport(Transform parent)
    {
        var body = UIFactory.Section(parent, "Exporter toutes les données", UITheme.Primaire, UITheme.PrimaireClair);
        _corps = body.transform;
        _section = body.transform.parent.gameObject;
        _section.SetActive(false);
    }

    public void Rafraichir()
    {
        var e = SessionCloud.Entreprise;
        bool voir = SessionCloud.Active && e != null && RegleExport.PeutExporter(e.EstAdmin, AbonnementCloud.Etat);
        _section.SetActive(voir);
        if (!voir || _enCours) return;

        Vider();
        // ponytail: le format est celui d'une entreprise CIPL sur le PC, mais l'import d'un export
        // (« Importer un export ») reste à faire : la phrase ne promet donc que la copie.
        Texte($"Une copie complète de {e.nom} sur ce PC : bâtiments, locataires, factures, baux, photos, charges, Réglages " +
              "(sans les mots de passe ni la clé Pennylane), rangés comme dans CIPL.",
              UITheme.Role.Aide, UITheme.TexteSecondaire);

        string dossier = null;
        var p = PlayerPrefs.GetString(Cle + e.id, "").Split('|');
        if (p.Length == 3 && long.TryParse(p[0], out long ticks))
        {
            dossier = p[1];
            Texte(RegleExport.DernierExport(new DateTime(ticks), p[2]), UITheme.Role.Donnee, UITheme.TextePrincipal);
            Texte(dossier, UITheme.Role.Aide, UITheme.TexteSecondaire);
        }

        var row = UIFactory.HBox(_corps, 8, false, "Boutons").transform;
        if (dossier != null && Directory.Exists(dossier)) Bouton(row, "Ouvrir le dossier", () => SaveIO.OpenFolder(dossier), false);
        if (HorsLigne.Actif) Texte("Export indisponible hors ligne : il relit toutes les fiches sur le cloud.", UITheme.Role.Aide, UITheme.TexteSecondaire);
        else Bouton(row, "Exporter…", Exporter, true);
    }

    async void Exporter()
    {
        if (!SaveIO.PickFolder("Où ranger l'export ?", out string parent)) return;
        var e = SessionCloud.Entreprise;
        if (e == null) return;
        _enCours = true;
        Vider();
        Texte($"Export de {e.nom} en cours…", UITheme.Role.Libelle, UITheme.TextePrincipal, true);
        var etat = Texte("Lecture des fiches sur le cloud…", UITheme.Role.Aide, UITheme.TexteSecondaire);
        ExportCloud.Resultat res;
        try { res = await ExportCloud.Exporter(parent, (fait, total) => { if (etat != null) etat.text = $"Documents : {fait} / {total}"; }); }
        catch (Exception ex) { res = new ExportCloud.Resultat { Erreur = ex.Message }; }
        _enCours = false;
        if (_corps == null) return;   // Play arrêté pendant l'export

        if (res.Erreur != null) ConfirmDialog.Erreur("Export impossible : " + res.Erreur);
        else
        {
            PlayerPrefs.SetString(Cle + e.id, $"{DateTime.Now.Ticks}|{res.Dossier}|{res.Bilan}");
            PlayerPrefs.Save();
            UndoToast.Instance?.ShowInfo($"Export terminé : {res.Bilan}.");
            if (res.Manquants.Count > 0)
                ConfirmDialog.Erreur($"{res.Manquants.Count} document(s) introuvable(s), même sur le cloud : " +
                                     string.Join(", ", res.Manquants.Take(10)) + (res.Manquants.Count > 10 ? "…" : "") +
                                     ". Tout le reste est exporté.");
        }
        Rafraichir();
    }

    void Vider() { foreach (Transform c in _corps) UnityEngine.Object.Destroy(c.gameObject); }

    TMP_Text Texte(string texte, float taille, Color couleur, bool gras = false) =>
        UIFactory.Text(_corps, texte, taille, couleur, gras);

    static void Bouton(Transform parent, string texte, Action clic, bool principal)
    {
        var b = principal ? UIFactory.Button(parent, texte, UITheme.Primaire, Color.white, 36, UITheme.Role.Action)
                          : UIFactory.Button(parent, texte, UITheme.Carte, UITheme.TextePrincipal, 36, UITheme.Role.Action, false);
        if (!principal) UIFactory.Border(b.gameObject);
        UIFactory.LargeurDuTexte(b);
        b.onClick.AddListener(() => clic());
    }
}

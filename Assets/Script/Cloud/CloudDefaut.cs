/// Revue 16c (D3-01) : le projet cloud de CIPL, compilé. Une installation neuve n'a pas le fichier
/// cipl_app_secrets.dat (écrit par CIPL › Essai cloud) : sans ces valeurs, CIPL ne démarrait pas chez un client.
/// Le fichier, s'il est renseigné, l'emporte (projet de test). La clé est la clé PUBLIQUE du projet, faite pour
/// être dans l'application : les droits sont ceux des règles du serveur (RLS). Changement de projet (étape 17) :
/// ces deux lignes, puis un nouveau build.
public static class CloudDefaut
{
    public const string Url = "https://ehxskfjmiioesmipzsad.supabase.co";
    public const string Cle = "sb_publishable_cynDY51GrkvADDC5nFo3Bw_9IARFllb";
}

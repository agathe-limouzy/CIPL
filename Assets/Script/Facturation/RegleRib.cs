using System.Linq;
using System.Text.RegularExpressions;

/// Retour du 25/10 (guide des premiers pas) : un RIB est contrôlé à « Valider ». Titulaire et IBAN
/// obligatoires, IBAN vérifié par sa clé, BIC de 8 ou 11 caractères s'il est saisi, n° de RIB
/// cohérent avec l'IBAN. Tout s'imprime sur les factures.
public static class RegleRib
{
    public const string ChampTitulaire = "titulaire", ChampRib = "rib", ChampIban = "iban", ChampBic = "bic";

    /// Lettres et chiffres seuls, en majuscules (« fr76 3000… » → « FR763000… »).
    public static string Compact(string s) => new string((s ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    /// Clé de contrôle de l'IBAN (ISO 13616, modulo 97) ; un IBAN français compte 27 caractères.
    public static bool IbanValide(string iban)
    {
        var s = Compact(iban);
        if (s.Length < 15 || s.Length > 34 || !char.IsLetter(s[0]) || !char.IsLetter(s[1]) || !char.IsDigit(s[2]) || !char.IsDigit(s[3])) return false;
        if (s.StartsWith("FR") && s.Length != 27) return false;
        int reste = 0;
        foreach (char c in s.Substring(4) + s.Substring(0, 4))
            foreach (char d in char.IsDigit(c) ? c.ToString() : (c - 'A' + 10).ToString())
                reste = (reste * 10 + (d - '0')) % 97;
        return reste == 1;
    }

    public static bool BicValide(string bic) => Regex.IsMatch(Compact(bic), "^[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?$");

    /// Le n° de RIB d'un IBAN français (banque, guichet, compte, clé) ; null pour un autre pays.
    public static string RibDepuisIban(string iban)
    {
        var s = Compact(iban);
        return s.StartsWith("FR") && s.Length == 27 ? $"{s.Substring(4, 5)} {s.Substring(9, 5)} {s.Substring(14, 11)} {s.Substring(25, 2)}" : null;
    }

    /// Null si le RIB peut être enregistré, sinon le motif ; `champs` : les zones à montrer en rouge.
    public static string Refus(string titulaire, string rib, string iban, string bic, out string[] champs)
    {
        champs = null;
        if (string.IsNullOrWhiteSpace(titulaire))
        { champs = new[] { ChampTitulaire }; return "Saisissez le titulaire du compte : il s'imprime sur les factures."; }
        if (!IbanValide(iban))
        { champs = new[] { ChampIban }; return "Cet IBAN n'est pas valide : recopiez-le depuis votre RIB (en France, 27 caractères commençant par FR)."; }
        if (!string.IsNullOrWhiteSpace(bic) && !BicValide(bic))
        { champs = new[] { ChampBic }; return "Le BIC compte 8 ou 11 lettres et chiffres (par exemple BNPAFRPP ou BNPAFRPPXXX)."; }
        var attendu = RibDepuisIban(iban);
        if (!string.IsNullOrWhiteSpace(rib) && attendu != null && Compact(rib) != Compact(attendu))
        { champs = new[] { ChampRib, ChampIban }; return "Le n° de RIB ne correspond pas à l'IBAN : vérifiez-les tous deux sur votre RIB."; }
        return null;
    }
}

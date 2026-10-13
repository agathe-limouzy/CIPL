using System;
using System.Runtime.InteropServices;

/// Chiffrement Windows lié au compte de l'utilisateur (DPAPI) : ce qui est protégé ici
/// n'est lisible que par le même compte Windows, sur le même PC. Sert à garder la
/// connexion (« Rester connectée sur ce PC ») sans laisser le jeton en clair.
/// System.Security.ProtectedData n'est pas disponible dans le profil .NET Standard
/// de Unity : appel direct de crypt32.dll.
public static class Dpapi
{
    [StructLayout(LayoutKind.Sequential)]
    struct Blob { public int taille; public IntPtr donnees; }

    const int SansInterface = 0x1;   // CRYPTPROTECT_UI_FORBIDDEN

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CryptProtectData(ref Blob entree, string description, IntPtr entropie,
                                        IntPtr reserve, IntPtr invite, int options, ref Blob sortie);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CryptUnprotectData(ref Blob entree, IntPtr description, IntPtr entropie,
                                          IntPtr reserve, IntPtr invite, int options, ref Blob sortie);

    [DllImport("kernel32.dll")]
    static extern IntPtr LocalFree(IntPtr memoire);

    public static byte[] Proteger(byte[] clair) => Appliquer(clair, true);

    /// Null si les octets ont été modifiés, ou viennent d'un autre compte ou d'un autre PC.
    public static byte[] Deproteger(byte[] chiffre) => Appliquer(chiffre, false);

    static byte[] Appliquer(byte[] octets, bool proteger)
    {
        if (octets == null || octets.Length == 0) return null;
        var entree = new Blob { taille = octets.Length, donnees = Marshal.AllocHGlobal(octets.Length) };
        var sortie = new Blob();
        try
        {
            Marshal.Copy(octets, 0, entree.donnees, octets.Length);
            bool ok = proteger
                ? CryptProtectData(ref entree, "CIPL", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, SansInterface, ref sortie)
                : CryptUnprotectData(ref entree, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, SansInterface, ref sortie);
            if (!ok) return null;
            var resultat = new byte[sortie.taille];
            Marshal.Copy(sortie.donnees, resultat, 0, sortie.taille);
            return resultat;
        }
        catch (Exception) { return null; }   // hors Windows, ou crypt32 indisponible
        finally
        {
            Marshal.FreeHGlobal(entree.donnees);
            if (sortie.donnees != IntPtr.Zero) LocalFree(sortie.donnees);
        }
    }
}

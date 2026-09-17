using NUnit.Framework;

/// Vérifie seulement que l'infrastructure de test fonctionne : NUnit accessible et
/// code du projet visible. Le code de CIPL vit dans `Assembly-CSharp` (aucun .asmdef),
/// et un assembly défini par .asmdef ne peut PAS référencer `Assembly-CSharp` — d'où
/// des tests placés sous `Assets/Editor/`, compilés dans `Assembly-CSharp-Editor`,
/// qui lui y a accès.
public class InfraTest
{
    [Test]
    public void NUnit_est_disponible()
    {
        Assert.That(2 + 2, Is.EqualTo(4));
    }

    [Test]
    public void Le_code_du_projet_est_visible()
    {
        Assert.That(SaisieNumerique.TryParse("3,5", out float v), Is.True);
        Assert.That(v, Is.EqualTo(3.5f).Within(0.0001f));
    }
}

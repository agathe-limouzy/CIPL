using UnityEngine;
using UnityEngine.EventSystems;
using System.Text.RegularExpressions;
using TMPro;

/// Rend un texte de contact cliquable : ouvre la boîte mail (mailto:) ou le
/// composeur téléphonique (tel:). À poser sur le TMP_Text d'affichage (« Text
/// afficher » d'un InputAndText) : n'est cliquable que quand ce texte est visible.
[RequireComponent(typeof(TMP_Text))]
public class ContactLink : MonoBehaviour, IPointerClickHandler
{
    public enum Mode { Email, Telephone }
    public Mode mode = Mode.Email;

    private TMP_Text _txt;

    private void Awake()
    {
        _txt = GetComponent<TMP_Text>();
        if (_txt != null) _txt.raycastTarget = true;
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (_txt == null) return;
        string v = _txt.text != null ? _txt.text.Trim() : "";
        if (string.IsNullOrEmpty(v)) return;

        if (mode == Mode.Email)
        {
            if (v.Contains("@")) Application.OpenURL("mailto:" + v);
        }
        else
        {
            string tel = Regex.Replace(v, "[^0-9+]", "");
            if (tel.Length >= 6) Application.OpenURL("tel:" + tel);
        }
    }
}

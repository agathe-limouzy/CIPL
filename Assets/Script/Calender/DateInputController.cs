using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.Events;

public class DateInputController : MonoBehaviour
{
    [Header("Inputs")]
    public TMP_InputField dayInput;
    public TMP_InputField monthInput;
    public TMP_InputField yearInput;
    public GameObject inputParent;



    [Header("UI Show Information")]
    public TMP_Text dateText;
    //  public Button confirmButton;

    // La date enregistrée, accessible depuis d'autres scripts
    public DateTime SelectedDate { get; private set; }
    public bool HasValidDate { get; private set; }
    public UnityEvent OnModify = new UnityEvent();

    private void Start()
    {
        // Limite les caractères
        dayInput.characterLimit = 2;
        monthInput.characterLimit = 2;
        yearInput.characterLimit = 4;

        // Clavier numérique uniquement
        dayInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        monthInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        yearInput.contentType = TMP_InputField.ContentType.IntegerNumber;

        // Placeholder
        SetPlaceholder(dayInput, "JJ");
        SetPlaceholder(monthInput, "MM");
        SetPlaceholder(yearInput, "AAAA");

        // Validation en temps réel
        dayInput.onEndEdit.AddListener(_ => ValidateDate());
        monthInput.onEndEdit.AddListener(_ => ValidateDate());
        yearInput.onEndEdit.AddListener(_ => ValidateDate());

        // Champ complet → le suivant : « 14 » passe au mois, « 05 » à l'année.
        dayInput.onValueChanged.AddListener(v => Suivant(dayInput, v, monthInput));
        monthInput.onValueChanged.AddListener(v => Suivant(monthInput, v, yearInput));

       // confirmButton.onClick.AddListener(OnConfirm);
      //  confirmButton.interactable = false;
    }

    // Seulement quand on tape (isFocused) : ApplyDate remplit les champs par code,
    // et afficher une fiche ne doit pas déplacer le curseur.
    private static void Suivant(TMP_InputField champ, string valeur, TMP_InputField suivant)
    {
        if (!champ.isFocused || valeur.Length < champ.characterLimit) return;
        suivant.Select();
        suivant.ActivateInputField();
    }

    private void SetPlaceholder(TMP_InputField field, string text)
    {
        var placeholder = field.placeholder.GetComponent<TMP_Text>();
        if (placeholder != null) placeholder.text = text;
    }

    private void ValidateDate()
    {
        HasValidDate = false;
       // confirmButton.interactable = false;

        if (string.IsNullOrEmpty(dayInput.text) ||
            string.IsNullOrEmpty(monthInput.text) ||
            string.IsNullOrEmpty(yearInput.text))
        {
            return;
        }

        if (!int.TryParse(dayInput.text, out int day) ||
            !int.TryParse(monthInput.text, out int month) ||
            !int.TryParse(yearInput.text, out int year))
        {
            return;
        }
        if (year < 1900) return;   // revue 16c (D7b-10) : même borne que LireDate (« 27 » n'est pas l'an 0027)

        // Vérifie que la date est valide
        try
        {
            SelectedDate = new DateTime(year, month, day);
            HasValidDate = true;
            OnModify.Invoke();
          //  confirmButton.interactable = true;
        }
        catch
        {
            HasValidDate = false;
        }
    }

    /// La date actuellement dans les champs, si elle est complète et valide — même
    /// avant la fin de la saisie (SelectedDate n'est mis à jour qu'en quittant un champ).
    public bool LireDate(out DateTime date)
    {
        date = default;
        if (!int.TryParse(dayInput.text, out int j) || !int.TryParse(monthInput.text, out int m)
            || !int.TryParse(yearInput.text, out int a) || a < 1900 || m < 1 || m > 12
            || j < 1 || j > DateTime.DaysInMonth(a, m)) return false;
        date = new DateTime(a, m, j);
        return true;
    }

    public DateTime saveThedate()
    {
        dateText.text= $"{SelectedDate:dd / MM / yyyy}";
        dateText.gameObject.SetActive( true );
        inputParent.gameObject.SetActive( false );
        return SelectedDate;

    }
    public void ApplyDate( DateTime date)
    {
        SelectedDate= date;
        dayInput.text = SelectedDate.Day.ToString();
        monthInput.text = SelectedDate.Month.ToString();
        yearInput.text = SelectedDate.Year.ToString();
        dateText.text = $"{SelectedDate:dd / MM / yyyy}";
        dateText.gameObject.SetActive(true);
        inputParent.SetActive(false);

    }

    /// Revue 17 (D7a-06) : les trois cases sont vides (« pas de date », autre chose qu'une date mal tapée).
    public bool Vide() => string.IsNullOrWhiteSpace(dayInput.text) && string.IsNullOrWhiteSpace(monthInput.text)
                          && string.IsNullOrWhiteSpace(yearInput.text);

    /// Revue 17 (D7a-06) : aucune date saisie : cases vides, `affichage` (une date proposée) à la place de la date.
    public void Vider(string affichage)
    {
        SelectedDate = default; HasValidDate = false;
        dayInput.text = monthInput.text = yearInput.text = "";
        dateText.text = affichage ?? "";
        dateText.gameObject.SetActive(true);
        inputParent.SetActive(false);
    }

    public void ModifyDate()
    {
        dateText.gameObject.SetActive(false);
        inputParent.SetActive(true);
    }



}

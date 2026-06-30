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

       // confirmButton.onClick.AddListener(OnConfirm);
      //  confirmButton.interactable = false;
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

    public bool  OnConfirm()
    {
        

        // Sauvegarde en PlayerPrefs (persiste entre les sessions)
       // PlayerPrefs.SetString("SavedDate", SelectedDate.ToString("yyyy-MM-dd"));
        //PlayerPrefs.Save();

        Debug.Log($"[Date] Enregistrée : {SelectedDate:yyyy-MM-dd}");
        return HasValidDate;
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

    public void ModifyDate()
    {
        dateText.gameObject.SetActive(false);
        inputParent.SetActive(true);
    }



// ── Appelle cette méthode depuis un autre script pour récupérer la date ──
public static DateTime LoadSavedDate()
    {
        string saved = PlayerPrefs.GetString("SavedDate", "");
        if (DateTime.TryParse(saved, out DateTime date))
            return date;
        return DateTime.Today;
    }
}

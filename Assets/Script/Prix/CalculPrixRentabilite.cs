using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

public class CalculPrixRentabilite : MonoBehaviour
{




    public TMP_InputField PrixPourRentabilite;
    public TMP_InputField LoyerPourRentabilite;
    public TMP_Text Rentabilité;

    public TMP_InputField PrixPourPrixAPaye;
    public TMP_InputField PourcentageDroitPourPrixAPaye;
    public TMP_InputField PourcentageAutreourPrixAPaye;
    public TMP_Text VraiPrixAPaye;


    public TMP_InputField LoyerAnnuelPourPrix;
    public TMP_InputField RentabilitePourPrix;
    public TMP_Text prixGraceARentabilite;

    public TMP_InputField PrixPourLoyer;
    public TMP_InputField RentabilitePourLoyer;
    public TMP_Text LoyerGraceARentabilité;

    public TMP_InputField PrixPourRentabilitéVrai;
    public TMP_InputField PourcentageDroitPourRentabliteVrai;
    public TMP_InputField PourcentageAutrePourRentabiliteVrai;
    public TMP_InputField LoyerAnnuelPourRentabiliteVrai;
    public TMP_Text RentabilitéVrai;

    public TMP_InputField LoyerPourLoyerM;
    public TMP_InputField MetreCarre;
    public TMP_Text LoyerMCarre;



    public void Rentabilite()
    {
        var LoyerAnnuel = CheckValueOnString(LoyerPourRentabilite);
        var prix = CheckValueOnString(PrixPourRentabilite);
        if(prix != 0f)
        {
            Rentabilité.text = CalculRentabilité(LoyerAnnuel, prix) + "%";
        }
        else
        {
            Rentabilité.text = "0 %";
        }
    }
    public float  CalculRentabilité( float loyerAnnuel , float prix)
    {
        return  (loyerAnnuel / prix) * 100f;
    }

    public void PrixAPayé()
    {
        var prix = CheckValueOnString(PrixPourPrixAPaye);
        var PourcentageDroit = CheckValueOnString(PourcentageDroitPourPrixAPaye);
        var PourcentageAutre = CheckValueOnString(PourcentageAutreourPrixAPaye);
        VraiPrixAPaye.text = CalculPrixAPayé(prix, PourcentageDroit, PourcentageAutre) +" €";
    }


    public float CalculPrixAPayé(float prix, float PourcentageDroit, float PourcentageAutre)
    {
        return  prix * (1 + PourcentageDroit / 100f + PourcentageAutre / 100f);

    }

    public void Prix()
    {
        var loyerAnnuel = CheckValueOnString(LoyerAnnuelPourPrix);
        var Rentabilite = CheckValueOnString(RentabilitePourPrix);
        prixGraceARentabilite.text = CalculPrixGraceARetabilité(loyerAnnuel,Rentabilite) + " €";
    }

    public float CalculPrixGraceARetabilité( float loyerAnnuel, float rentabilité)
    {
        return  (loyerAnnuel / rentabilité) * 100f;
    }

    public void LoyerAnnuel()
    {
        var Rentabilite = CheckValueOnString(RentabilitePourLoyer);
        var prix = CheckValueOnString(PrixPourLoyer);
        LoyerGraceARentabilité.text = CalculLoyerGraceARentabilité(prix, Rentabilite) + " €";



    }
    public float CalculLoyerGraceARentabilité( float prix, float rentabilité)
    {
        return  (prix / 100f) * rentabilité;
   
    }

    public void VraiRentabilite()
    {
        var loyerAnnuel = CheckValueOnString(LoyerAnnuelPourRentabiliteVrai);
        var prix = CheckValueOnString(PrixPourRentabilitéVrai);
        var PourcentageDroit = CheckValueOnString(PourcentageDroitPourRentabliteVrai);
        var PourcentageAutre = CheckValueOnString(PourcentageAutrePourRentabiliteVrai);

        if (prix != 0f)
        {
            RentabilitéVrai.text = CalculRentabilitéVraiPrix(loyerAnnuel, prix, PourcentageDroit, PourcentageAutre) + " %";
        }
        else
        {
            RentabilitéVrai.text = "0 %";
        }



    }

    public float CalculRentabilitéVraiPrix(float loyerAnnuel, float prix, float PourcentageDroit, float PourcentageAutre)
    {
        return  (loyerAnnuel / CalculPrixAPayé(prix, PourcentageDroit, PourcentageAutre)) * 100f;
    }


    public void LoyerAuMCarre()
    {
        var Loyer = CheckValueOnString(LoyerPourLoyerM);
        var metre = CheckValueOnString(MetreCarre);
        if (metre != 0f)
        {
            LoyerMCarre.text = CalculPrixALoyerCarré(Loyer, metre) + " €";
        }
        else
        {
            LoyerMCarre.text = "0 €";
        }


    }

    public float CalculPrixALoyerCarré(float prix , float metrecarre)
    {
        return prix / metrecarre;
    }


    public float CheckValueOnString(TMP_InputField origin)
    {
        string text = origin.text;
        if (!string.IsNullOrEmpty(text))
        {
            if (float.TryParse(text,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float result))
            {
                 return result;
            }
            else
            {
               return 0f;
            }
        }
        else
        {
            return 0f;
        }
    }

    private void Start()
    {
        PrixPourRentabilite.onSubmit.AddListener(_ => Rentabilite());
        LoyerPourRentabilite.onSubmit.AddListener(_ => Rentabilite());

        PrixPourPrixAPaye.onSubmit.AddListener(_ => PrixAPayé());
        PourcentageDroitPourPrixAPaye.onSubmit.AddListener(_ => PrixAPayé());
        PourcentageAutreourPrixAPaye.onSubmit.AddListener(_ => PrixAPayé());

        LoyerAnnuelPourPrix.onSubmit.AddListener(_ => Prix());
        RentabilitePourPrix.onSubmit.AddListener(_ => Prix());

        PrixPourLoyer.onSubmit.AddListener(_ => LoyerAnnuel());
        RentabilitePourLoyer.onSubmit.AddListener(_ => LoyerAnnuel());

        LoyerAnnuelPourRentabiliteVrai.onSubmit.AddListener(_ => VraiRentabilite());
        PourcentageDroitPourRentabliteVrai.onSubmit.AddListener(_ => VraiRentabilite());
        PourcentageAutrePourRentabiliteVrai.onSubmit.AddListener(_ => VraiRentabilite());
        PrixPourRentabilitéVrai.onSubmit.AddListener(_ => VraiRentabilite());

        LoyerPourLoyerM.onSubmit.AddListener(_ => LoyerAuMCarre());
        MetreCarre.onSubmit.AddListener(_ => LoyerAuMCarre());

        gameObject.SetActive(false);


    }





}


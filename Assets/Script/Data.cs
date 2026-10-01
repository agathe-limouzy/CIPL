using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Data 
{
    public const string NomParDefaut = "Nouveau";   // fiche jamais nommée
    public string id = Guid.NewGuid().ToString();
    public string Name = NomParDefaut;
}

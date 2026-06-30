using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Data 
{
    public string id = Guid.NewGuid().ToString();
    public string Name = "Nouveau";
}

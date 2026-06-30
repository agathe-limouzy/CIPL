using System;

[Serializable]
public class Objective
{
    public string id;
    public string text;
    public ObjectiveStatus status;
    public DateTime createdAt;

    public enum ObjectiveStatus
    {
        AFaire,
        EnCours,
        Fait,
        Obligatoire,
        Rappel
    }

    public Objective(string text, ObjectiveStatus status = ObjectiveStatus.AFaire)
    {
        id = Guid.NewGuid().ToString();
        this.text = text;
        this.status = status;
        createdAt = DateTime.Now;
    }
}

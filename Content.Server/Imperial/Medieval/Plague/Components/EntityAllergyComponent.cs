namespace Content.Server.Imperial.Medieval.Plague;

[RegisterComponent]
public sealed partial class EntityAllergyComponent : Component
{
    [DataField]
    public List<string> Ids = new();

    [DataField]
    public List<string> RandomIds = new();

    [DataField]
    public float Distance = 3.5f;

    [DataField]
    public int RandomCount = 1;
}

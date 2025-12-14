namespace Content.Server.Imperial.Medieval.Plague;

[RegisterComponent]
public sealed partial class MedievalPlagueInfectOnHitComponent : Component
{
    [DataField]
    public bool Active = true;

    [DataField]
    public string Id = "";

    [DataField]
    public float Chance = 0.05f;
}

namespace Content.Server.Imperial.Lavaland.Golem;

[RegisterComponent]
public sealed partial class GolemShellComponent : Component
{
    [DataField(required: true)]
    public Dictionary<string, string> MaterialToGolem = new();

    [DataField]
    public int RequiredCount = 30;

    [ViewVariables]
    public string? CurrentMaterial;

    [ViewVariables]
    public int MaterialCount;
}

namespace Content.Server.Imperial.Medieval.Plague;

[RegisterComponent]
public sealed partial class LoweredSkillsComponent : Component
{
    [DataField]
    public int LoweredBy = 2;

    public Dictionary<string, int> OriginalLevels = new();
}


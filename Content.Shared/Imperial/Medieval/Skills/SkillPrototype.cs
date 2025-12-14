using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Skills;

[Prototype]
public sealed partial class SkillPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = default!;

    [DataField(required: true)]
    public string Name { get; } = default!;

    [DataField]
    public Color Color = Color.White;

    [DataField]
    public Dictionary<string, float> Modifiers = new();

    [DataField]
    public string RsiPath = "/Textures/Interface/Misc/job_icons.rsi";

    [DataField]
    public Dictionary<int, string> Icons = new()
    {
        { 1, "Admin" }
    };
}

using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenomorph;

/// <summary>Лицехват (clothing/mask/facehugger).</summary>
[RegisterComponent]
public sealed partial class XenoFacehuggerComponent : Component
{
    [DataField]
    public bool Sterile;

    [DataField]
    public float LeapRange = 1.5f;

    [DataField]
    public TimeSpan MinActive = TimeSpan.FromSeconds(20);

    [DataField]
    public TimeSpan MaxActive = TimeSpan.FromSeconds(40);

    [DataField]
    public TimeSpan MinImpregnation = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan MaxImpregnation = TimeSpan.FromSeconds(15);

    [DataField]
    public DamageSpecifier AttachDamage = new() { DamageDict = new() { ["Blunt"] = 5 } };

    /// <summary>Attach: Paralyze 1 с и Knockdown 10 с.</summary>
    [DataField]
    public TimeSpan AttachKnockdown = TimeSpan.FromSeconds(10);

    [ViewVariables] public Shared.Imperial.Xenomorph.XenoFacehuggerState State;
    [ViewVariables] public TimeSpan NextStateAt;
    [ViewVariables] public EntityUid? Victim;
    [ViewVariables] public TimeSpan ImpregnateAt;
    [ViewVariables] public TimeSpan NextCheck;

    [DataField]
    public TimeSpan AttachDuration = TimeSpan.FromSeconds(1);
}

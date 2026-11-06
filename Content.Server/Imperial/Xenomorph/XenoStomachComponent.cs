using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenomorph;

/// <summary>Желудок ксеноморфа: пожранные (devour) перевариваются.</summary>
[RegisterComponent]
public sealed partial class XenoStomachComponent : Component
{
    public const string ContainerId = "xeno_stomach";

    [DataField]
    public TimeSpan DevourTime = TimeSpan.FromSeconds(13.5);

    /// <summary>stomach_acid_power = 75: acid_act(75, 10) по содержимому раз в три тика Life (6 с).</summary>
    [DataField]
    public DamageSpecifier Digestion = new() { DamageDict = new() { ["Caustic"] = 75 } };

    [DataField]
    public TimeSpan DigestInterval = TimeSpan.FromSeconds(6);

    [DataField]
    public SoundSpecifier RegurgitateSound = new SoundPathSpecifier("/Audio/Imperial/Xenomorph/alien_york.ogg");

    [DataField]
    public SoundSpecifier DevourSound = new SoundPathSpecifier("/Audio/Imperial/Xenomorph/alien_eat.ogg");

    [ViewVariables] public TimeSpan NextDigest;
}

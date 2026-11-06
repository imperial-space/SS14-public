using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>clamp: грузит ящики в модуль и выгружает их.</summary>
[RegisterComponent]
public sealed partial class ImperialModClampComponent : Component
{
    public const string ContainerId = "imperial-mod-crates";

    /// <summary>grabDelay грейфера меха.</summary>
    [DataField]
    public TimeSpan LoadTime = TimeSpan.FromSeconds(2.5);

    /// <summary>maxContents грейфера меха.</summary>
    [DataField]
    public int MaxCrates = 10;

    /// <summary>Энергия МОДа на захват (grabEnergyDelta).</summary>
    [DataField]
    public float GrabPower = 3f;

    [DataField]
    public Robust.Shared.Audio.SoundSpecifier GrabSound = new Robust.Shared.Audio.SoundPathSpecifier("/Audio/Mecha/sound_mecha_hydraulic.ogg");

    [ViewVariables]
    public EntityUid? AudioStream;
}

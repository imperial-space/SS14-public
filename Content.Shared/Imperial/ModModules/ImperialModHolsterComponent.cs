using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>holster: хранит одно оружие и выдаёт его в руку.</summary>
[RegisterComponent]
public sealed partial class ImperialModHolsterComponent : Component
{
    public const string ContainerId = "imperial-mod-holster";

    [DataField]
    public Robust.Shared.Audio.SoundSpecifier Sound = new Robust.Shared.Audio.SoundPathSpecifier("/Audio/Weapons/Guns/Empty/empty.ogg");
}

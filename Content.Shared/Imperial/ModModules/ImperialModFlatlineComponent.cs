using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>status_readout: динамик костюма громко сообщает о смерти носителя.</summary>
[RegisterComponent]
public sealed partial class ImperialModFlatlineComponent : Component
{
    [DataField]
    public Robust.Shared.Audio.SoundSpecifier Sound = new Robust.Shared.Audio.SoundPathSpecifier("/Audio/Imperial/ModModules/flatline.ogg");
}

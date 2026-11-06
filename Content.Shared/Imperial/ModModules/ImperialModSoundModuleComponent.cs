using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>bikehorn: звук при применении.</summary>
[RegisterComponent]
public sealed partial class ImperialModSoundModuleComponent : Component
{
    [DataField(required: true)]
    public Robust.Shared.Audio.SoundSpecifier Sound = default!;
}

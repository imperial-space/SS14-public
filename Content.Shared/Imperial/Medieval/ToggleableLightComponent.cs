using Robust.Shared.Audio;
using Content.Shared.Imperial.ToggleableLight.EntitySystems;

namespace Content.Shared.Imperial.ToggleableLight.Components;

[RegisterComponent]
[Access(typeof(ToggleableLightSystem))]
public sealed partial class ToggleableLightComponent : Component
{
    [DataField("turnOnSound")]
    public SoundSpecifier? TurnOnSound;

    [DataField("turnOffSound")]
    public SoundSpecifier? TurnOffSound;
}

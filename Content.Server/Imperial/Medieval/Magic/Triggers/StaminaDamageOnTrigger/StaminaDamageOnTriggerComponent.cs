using Robust.Shared.Audio;

namespace Content.Server.Imperial.Medieval.Magic.Triggers;

/// <summary>
/// Stamina damage user on trigger
/// </summary>
[RegisterComponent, Access(typeof(StaminaDamageOnTriggerSystem))]
public sealed partial class StaminaDamageOnTriggerComponent : Component
{
    [DataField]
    public float Damage = 55f;

    [DataField]
    public SoundSpecifier? Sound;
}

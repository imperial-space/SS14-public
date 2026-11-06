using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared.Imperial.Heretic.Paths.Rust;

[RegisterComponent, AutoGenerateComponentPause, NetworkedComponent]
public sealed partial class HereticRustDashComponent : Component
{
    [DataField] public float DashSpeed = 18f;
    [DataField] public TimeSpan DashWaitDuration = TimeSpan.FromSeconds(0.5f);
    [DataField] public TimeSpan TrailInterval = TimeSpan.FromSeconds(0.1f);
    [DataField] public TimeSpan RustInterval = TimeSpan.FromSeconds(0.15f);
    [DataField] public TimeSpan DamageInterval = TimeSpan.FromSeconds(0.25f);
    [DataField] public float HitRadius = 0.9f;
    [DataField] public float DashDamage = 30f;
    [DataField] public string DashTrailPrototype = "HereticRustDashTrail";
    [DataField] public string DashMarkerPrototype = "BubblegumDashMarker";
    [DataField] public SoundSpecifier DashSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_weapons_kenetic_accel.ogg");

    [ViewVariables]
    public bool IsWaiting;
    [ViewVariables]
    public bool IsActive;
    [ViewVariables]
    public EntityCoordinates Destination;
    [ViewVariables, AutoPausedField]
    public TimeSpan WaitEnd;
    [ViewVariables, AutoPausedField]
    public TimeSpan NextTrail;
    [ViewVariables, AutoPausedField]
    public TimeSpan NextRust;
    [ViewVariables, AutoPausedField]
    public TimeSpan NextDamage;
}

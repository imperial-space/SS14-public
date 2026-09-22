using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticRustDashComponent : Component
{
    [DataField] public float DashSpeed = 18f;
    [DataField] public float DashWaitDuration = 0.5f;
    [DataField] public float TrailInterval = 0.1f;
    [DataField] public float RustInterval = 0.15f;
    [DataField] public float DamageInterval = 0.25f;
    [DataField] public float HitRadius = 0.9f;
    [DataField] public float DashDamage = 30f;
    [DataField] public string DashTrailPrototype = "HereticRustDashTrail";
    [DataField] public string DashMarkerPrototype = "BubblegumDashMarker";
    [DataField] public SoundSpecifier DashSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_weapons_kenetic_accel.ogg");

    public bool IsWaiting;
    public bool IsActive;
    public EntityCoordinates Destination;
    public float WaitAccum;
    public float TrailAccum;
    public float RustAccum;
    public float DamageAccum;
}

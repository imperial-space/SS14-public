using Robust.Shared.Audio;


[RegisterComponent]
public sealed partial class UniversalLockableComponent : Component
{
    // Sounds
    [DataField]
    public SoundPathSpecifier? ActivateInWorldDenySound = new SoundPathSpecifier("/Audio/Imperial/Medieval/lyazg-visyaschego-zamka-na-zakryitoy-dveri.ogg");
    [DataField]
    public SoundPathSpecifier? InteractUsingDenySound = null;


    [DataField]
    public SoundPathSpecifier? LockUnlockedSound = new SoundPathSpecifier("/Audio/Imperial/Medieval/door_lock1.ogg");

    [DataField]
    public SoundPathSpecifier? LockLockedSound = new SoundPathSpecifier("/Audio/Imperial/Medieval/door_lock2.ogg");
}

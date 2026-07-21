using Content.Shared.Movement.Pulling.Components;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;


namespace Content.Shared.Imperial.Medieval.UniversalSecurity;

[RegisterComponent]
public sealed partial class UniversalLockComponent : Component
{

    [DataField]
    public bool IsLocked = false;

    [DataField]
    public bool IsSetuped { get; set; } = false;

    [DataField]
    public String Name = String.Empty;

    [DataField]
    public float TimeToEject = 10f;


    // Sound
    [DataField]
    public SoundPathSpecifier LockSetupSound = new SoundPathSpecifier("/Audio/Imperial/Medieval/lockpick_next.ogg");

    // Hack
    [DataField]
    public int MaxValue = 8;

    [DataField]
    public int[] Code = new int[3];
    [DataField]
    public int Length = 4;
}

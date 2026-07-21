using Robust.Shared.Audio;

[RegisterComponent]
public sealed partial class UniversalKeyComponent : Component
{
    [DataField]
    public float DoAfterSetupTime = 5;

    [DataField]
    public int[] Code = new int[3];

    [DataField]
    public bool IsSetuped = false;

    [DataField]
    public bool IsSuperKey = false;

    [DataField]
    public int MaxToothValue = 100;

    [DataField]
    public int MaxTeethCount = 100;

    [DataField]
    public String Name = String.Empty;

    [DataField]
    public EntityUid? User;
    public EntityUid? Knife;

    [DataField]
    public SoundPathSpecifier KeySetupSound = new SoundPathSpecifier("/Audio/Imperial/Medieval/lockpick_next.ogg");
}

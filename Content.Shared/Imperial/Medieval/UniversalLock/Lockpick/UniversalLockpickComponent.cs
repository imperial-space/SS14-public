[RegisterComponent]
public sealed partial class UniversalLockpickComponent : Component
{
    [DataField]
    public float BreakChance = 0.5f;

    [DataField]
    public float HackTime = 1f;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public string EffectSoundOnOpen = "/Audio/Imperial/Medieval/lockpick_open.ogg";

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public string EffectSoundOnSucces = "/Audio/Imperial/Medieval/lockpick_succes.ogg";

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public string EffectSoundOnNext = "/Audio/Imperial/Medieval/lockpick_next.ogg";

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public string EffectSoundOnBreak = "/Audio/Imperial/Medieval/lockpick_break.ogg";

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public string EffectSoundOnNo = "/Audio/Imperial/Medieval/lockpick_no.ogg";

    public EntityUid? LockUid;
    public EntityUid? LockableUid;
    public EntityUid? User;
}

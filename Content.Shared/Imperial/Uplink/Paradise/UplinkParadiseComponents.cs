using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Paradise;

/// <summary>Mind Batterer (Paradise, /obj/item/batterer): бьёт по разуму всех вокруг.</summary>
[RegisterComponent]
public sealed partial class UplinkMindBattererComponent : Component
{
    [DataField]
    public int MaxUses = 5;

    [DataField]
    public float Range = 7f;

    /// <summary>Одна зарядка восстанавливается примерно за 20 секунд.</summary>
    [DataField]
    public TimeSpan RechargeTime = TimeSpan.FromSeconds(20);

    [ViewVariables]
    public int TimesUsed;

    [ViewVariables]
    public TimeSpan NextRecharge;

    [ViewVariables]
    public TimeSpan ThrowCooldownUntil;
}

[Serializable, NetSerializable]
public sealed partial class UplinkBattererDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public enum UplinkBattererVisuals : byte
{
    Burnt,
}

/// <summary>Временное умиротворение от Mind Batterer (status_effect/pacifism/batterer).</summary>
[RegisterComponent]
public sealed partial class UplinkBattererPacifiedComponent : Component
{
    [ViewVariables]
    public TimeSpan EndTime;
}

/// <summary>Граната всплывающей турели: турель не стреляет в того, кто выдернул чеку.</summary>
[RegisterComponent]
public sealed partial class UplinkPopupTurretGrenadeComponent : Component
{
    [DataField]
    public EntProtoId Turret = "ImperialPopupTurret";

    [ViewVariables]
    public EntityUid? Owner;
}

/// <summary>Гаррота из оптоволокна (Paradise, /obj/item/garrote).</summary>
[RegisterComponent]
public sealed partial class UplinkGarroteComponent : Component
{
    /// <summary>Урон удушьем за тик удушения (process раз в ~2 секунды).</summary>
    [DataField]
    public float OxyDamage = 4f;

    [DataField]
    public TimeSpan TickTime = TimeSpan.FromSeconds(2);

    [ViewVariables]
    public EntityUid? Strangling;

    [ViewVariables]
    public TimeSpan NextTick;

    [ViewVariables]
    public TimeSpan CooldownUntil;
}

/// <summary>Жертва гарроты: почти не может сдвинуться.</summary>
[RegisterComponent]
public sealed partial class UplinkGarrotedComponent : Component
{
    [ViewVariables]
    public EntityUid Garrote;
}

[Serializable, NetSerializable]
public enum UplinkGarroteVisuals : byte
{
    Strangling,
}

/// <summary>Кейс с ложным дном (Paradise, /obj/item/storage/briefcase/false_bottomed).</summary>
[RegisterComponent]
public sealed partial class UplinkFalseBottomComponent : Component
{
    [DataField]
    public string Container = "false_bottom";

    [DataField]
    public TimeSpan PryTime = TimeSpan.FromSeconds(2);

    [ViewVariables]
    public bool Open;

    [ViewVariables]
    public bool Busy;
}

[Serializable, NetSerializable]
public sealed partial class UplinkFalseBottomDoAfterEvent : SimpleDoAfterEvent;

using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ReactiveArmor;

/// <summary>
/// clothing/suit/armor/reactive/teleport (tgstation/BandaStation, reactive_armour.dm):
/// при попадании отбрасывает владельца телепортом на несколько клеток в сторону.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ReactiveTeleportArmorComponent : Component
{
    /// <summary>active: броня реагирует на удары.</summary>
    [DataField, AutoNetworkedField]
    public bool Active;

    /// <summary>hit_reaction_chance.</summary>
    [DataField]
    public float ReactionChance = 0.5f;

    /// <summary>tele_range.</summary>
    [DataField]
    public int TeleportRange = 6;

    /// <summary>reactivearmor_cooldown_duration.</summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(10);

    [ViewVariables]
    public TimeSpan NextActivation;

    /// <summary>bad_effect: после ЭМИ броня 30 секунд работает против владельца.</summary>
    [DataField]
    public TimeSpan EmpDuration = TimeSpan.FromSeconds(30);

    [ViewVariables]
    public TimeSpan EmpEndsAt;

    [ViewVariables]
    public EntityUid? Wearer;

    [DataField]
    public EntProtoId ToggleAction = "ActionToggleReactiveArmor";

    [DataField]
    public EntityUid? ToggleActionEntity;
}

/// <summary>reactive_armor_shell: оболочка, которая ждёт ядро аномалии.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ReactiveArmorShellComponent : Component
{
    /// <summary>anomaly_armour_types: какое ядро во что превращает оболочку.</summary>
    [DataField]
    public Dictionary<EntProtoId, EntProtoId> Armors = new();
}

/// <summary>datum/action/item_action/toggle.</summary>
public sealed partial class ToggleReactiveArmorEvent : InstantActionEvent;

[Serializable, NetSerializable]
public enum ReactiveArmorVisuals : byte
{
    Active,
}

using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>
/// Модуль МОДа (/obj/item/mod/module).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialModModuleComponent : Component
{
    [DataField]
    public ImperialModModuleType ModuleType = ImperialModModuleType.Passive;

    /// <summary>complexity: сколько места модуль занимает в костюме.</summary>
    [DataField]
    public int Complexity;

    /// <summary>idle_power_cost в долях DEFAULT_CHARGE_DRAIN за секунду.</summary>
    [DataField]
    public float IdlePower;

    /// <summary>active_power_cost в долях DEFAULT_CHARGE_DRAIN за секунду.</summary>
    [DataField]
    public float ActivePower;

    /// <summary>use_energy_cost в долях DEFAULT_CHARGE_DRAIN за применение.</summary>
    [DataField]
    public float UsePower;

    /// <summary>cooldown_time.</summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.Zero;

    /// <summary>
    /// incompatible_modules: модули с общей группой нельзя поставить вместе.
    /// </summary>
    [DataField]
    public List<string> Groups = new();

    /// <summary>required_slots.</summary>
    [DataField]
    public ImperialModPart RequiredParts = ImperialModPart.Back;

    /// <summary>Работает при неактивном костюме (MODULE_ALLOW_INACTIVE).</summary>
    [DataField]
    public bool AllowInactive;

    /// <summary>Часть костюма, на которую вешаются <see cref="PartComponents"/>.</summary>
    [DataField]
    public ImperialModPart EffectPart = ImperialModPart.Back;

    /// <summary>Компоненты на часть костюма, пока эффект модуля включён (HUD, защита глаз и т. п.).</summary>
    [DataField]
    public ComponentRegistry PartComponents = new();

    /// <summary>Компоненты на носителя, пока эффект модуля включён.</summary>
    [DataField]
    public ComponentRegistry WearerComponents = new();

    /// <summary>device: предмет, который активный модуль выдаёт в руку.</summary>
    [DataField]
    public EntProtoId? Device;

    [ViewVariables]
    public EntityUid? DeviceEntity;

    [DataField]
    public EntProtoId Action = "ImperialActionModModule";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    /// <summary>active: модуль включён (переключаемый или выбранный).</summary>
    [DataField, AutoNetworkedField]
    public bool Active;

    [ViewVariables]
    public TimeSpan NextUse;

    /// <summary>Эффект сейчас наложен (компоненты выданы).</summary>
    [ViewVariables]
    public bool EffectApplied;

    /// <summary>Какие компоненты модуль реально добавил и куда (чтобы не снять чужие).</summary>
    [ViewVariables]
    public List<(EntityUid Target, Type Type)> AppliedComponents = new();
}

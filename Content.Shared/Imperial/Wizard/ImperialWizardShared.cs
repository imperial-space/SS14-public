using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

/// <summary>Маг или ученик (TRAIT_MAGICALLY_GIFTED): видит освящённые клетки, может пользоваться магическими предметами.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialMagicallyGiftedComponent : Component;

/// <summary>Заклинание «Телепорт» (area_teleport/wizard): выбор зоны станции.</summary>
public sealed partial class ImperialAreaTeleportActionEvent : InstantActionEvent;

/// <summary>Сервер → клиент: tgui_input_list — выбрать пункт из списка.</summary>
[Serializable, NetSerializable]
public sealed class ImperialWizardListInputEvent(int id, string title, string message, List<string> options) : EntityEventArgs
{
    public int Id = id;
    public string Title = title;
    public string Message = message;
    public List<string> Options = options;
}

/// <summary>Клиент → сервер: выбранный пункт (null — отмена).</summary>
[Serializable, NetSerializable]
public sealed class ImperialWizardListChoiceEvent(int id, string? choice) : EntityEventArgs
{
    public int Id = id;
    public string? Choice = choice;
}

/// <summary>Заклинание без цели (self/aoe/conjure): эффект задают компоненты на действии.</summary>
public sealed partial class ImperialSpellInstantEvent : InstantActionEvent;

/// <summary>Заклинание на существо (pointed).</summary>
public sealed partial class ImperialSpellEntityEvent : EntityTargetActionEvent;

/// <summary>Заклинание в точку (projectile).</summary>
public sealed partial class ImperialSpellWorldEvent : WorldTargetActionEvent;

/// <summary>Рука заклинания касания (/obj/item/melee/touch_attack).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialTouchHandComponent : Component
{
    [ViewVariables]
    public EntityUid? Spell;
}

/// <summary>Лазерное зрение (laser_eyes) от «Мутации».</summary>
public sealed partial class ImperialLaserEyesActionEvent : WorldTargetActionEvent;

/// <summary>Действует «Мутация» мага: зелёный цвет халка, гигантизм, лазерные глаза.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialWizardMutatedComponent : Component
{
    [ViewVariables]
    public TimeSpan End;

    [ViewVariables]
    public EntityUid? LaserAction;
}

/// <summary>Дух переходит в бестелесную форму и обратно (Forsake Body).</summary>
public sealed partial class ImperialSpiritPhaseActionEvent : InstantActionEvent;

/// <summary>Уменьшен (посох/палочка уменьшения).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialShrunkComponent : Component;

/// <summary>Оживлённый предмет (animate_atom_living): облик берётся с прототипа предмета.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ImperialAnimatedComponent : Component
{
    [DataField, AutoNetworkedField]
    public string? Proto;
}

[Serializable, NetSerializable]
public enum ImperialSoulstoneVisuals : byte
{
    Full,
}

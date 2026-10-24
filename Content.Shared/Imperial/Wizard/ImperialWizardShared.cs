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

/// <summary>Перк «Прозрачность»: маг полупрозрачен.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialTransparentVisualsComponent : Component;

/// <summary>Призыв, отталкивание или подрыв рунического торгомата скипетром.</summary>
[Serializable, NetSerializable]
public sealed partial class ImperialArtefactDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;

/// <summary>Произнесение заклинания с поддержанием (каналом).</summary>
[Serializable, NetSerializable]
public sealed partial class ImperialSpellChannelDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;

/// <summary>/datum/action/cooldown/grand_ritual: указать узел силы или нарисовать руну.</summary>
public sealed partial class ImperialGrandRitualActionEvent : InstantActionEvent;

/// <summary>Рисование руны Великого ритуала (4 секунды).</summary>
[Serializable, NetSerializable]
public sealed partial class ImperialGrandRuneDrawDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;

/// <summary>Одно из трёх произнесений руны.</summary>
[Serializable, NetSerializable]
public sealed partial class ImperialGrandRuneInvokeDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;

/// <summary>jaunt/bloodcrawl: войти в лужу крови или выйти из неё.</summary>
public sealed partial class ImperialBloodCrawlActionEvent : InstantActionEvent;

/// <summary>Удар-бросок демона резни (alt-атака TG).</summary>
public sealed partial class ImperialDemonSlamActionEvent : EntityTargetActionEvent;

/// <summary>Выход из кровавой лужи (2 секунды).</summary>
[Serializable, NetSerializable]
public sealed partial class ImperialBloodCrawlExitDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;

/// <summary>Одна из трёх стадий пожирания жертвы в крови.</summary>
[Serializable, NetSerializable]
public sealed partial class ImperialDemonConsumeDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;

/// <summary>Выйти из наблюдения через консоль логова (camera_advanced).</summary>
public sealed partial class ImperialScryingExitActionEvent : InstantActionEvent;

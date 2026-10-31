using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Тело Астартес: ларраманов орган останавливает кровотечение, сус-ан мембрана лечит в критическом состоянии,
/// преомнор защищает от ядов, оссмодула — от переломов, чёрный панцирь нужен для силового доспеха.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CadiaAstartesComponent : Component
{
    /// <summary>Ларраманов орган: сколько кровотечения снимается в секунду.</summary>
    [DataField]
    public float BleedStop = 1f;

    /// <summary>Сус-ан: сколько урона лечится в секунду в критическом состоянии.</summary>
    [DataField]
    public float AnabiosisHeal = 1.5f;

    /// <summary>Прогеноид ещё не извлечён.</summary>
    [DataField]
    public bool HasProgenoid = true;
}

/// <summary>Силовой доспех: надевается только поверх чёрного панциря Астартес.</summary>
[RegisterComponent]
public sealed partial class CadiaPowerArmorComponent : Component
{
    /// <summary>Доспех сестёр битвы шьют на людей: панцирь ему не нужен.</summary>
    [DataField]
    public bool RequiresCarapace = true;
}

/// <summary>Редуктор апотекария: извлекает прогеноид из павшего десантника.</summary>
[RegisterComponent]
public sealed partial class CadiaReductorComponent : Component;

/// <summary>Геносемя: своё даёт ещё одно место десантника в следующей волне, Хаосу — силу варпа.</summary>
[RegisterComponent]
public sealed partial class CadiaGeneSeedComponent : Component
{
    [DataField]
    public int WarpValue = 15;
}

[Serializable, NetSerializable]
public sealed partial class CadiaReductorDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Прыжковый ранец: удар с воздуха и подъём на соседний этаж.</summary>
[RegisterComponent]
public sealed partial class CadiaJumpPackComponent : Component
{
    [DataField]
    public float Range = 8f;

    [DataField]
    public float ImpactRadius = 1.8f;

    [DataField]
    public float ImpactDamage = 30f;

    /// <summary>Когда приземлиться (идёт прыжок).</summary>
    [ViewVariables]
    public TimeSpan? LandAt;

    [ViewVariables]
    public EntityUid? Jumper;

    /// <summary>Кому ранец дал полёт (как у Хелдрейка: не падает в проёмы, сам ходит между этажами).</summary>
    [ViewVariables]
    public EntityUid? Flyer;
}

public sealed partial class CadiaJumpActionEvent : WorldTargetActionEvent;

/// <summary>Вера священника: молитва снимает напряжение псайкеров и лечит рядом.</summary>
public sealed partial class CadiaPrayerActionEvent : InstantActionEvent;

/// <summary>Акт веры сестры битвы: на время почти не чувствует боли.</summary>
public sealed partial class CadiaActOfFaithActionEvent : InstantActionEvent;

/// <summary>Под действием Акта веры.</summary>
[RegisterComponent]
public sealed partial class CadiaFaithShieldComponent : Component
{
    [ViewVariables]
    public TimeSpan Until;
}

/// <summary>Экстерминатус инквизитора: красная кнопка, отдаёт Кадию огню, лишь бы не врагу.</summary>
public sealed partial class CadiaExterminatusActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class CadiaExterminatusDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Роль с дополнительными действиями при появлении.</summary>
[RegisterComponent]
public sealed partial class CadiaRoleActionsComponent : Component
{
    [DataField]
    public List<Robust.Shared.Prototypes.EntProtoId> Actions = new();
}

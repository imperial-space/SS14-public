using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Genetics;

/// <summary>Телекинез: притянуть предмет в руку или отбросить существо.</summary>
public sealed partial class GeneticTelekinesisActionEvent : EntityTargetActionEvent;

/// <summary>Электрокасание (shock): разряд в цель рядом.</summary>
public sealed partial class GeneticShockTouchActionEvent : EntityTargetActionEvent;

/// <summary>Исцеляющее касание (lay_on_hands): лечит цель, часть урона забирает себе.</summary>
public sealed partial class GeneticMendingTouchActionEvent : EntityTargetActionEvent;

/// <summary>Телепатия: мысленное сообщение цели.</summary>
public sealed partial class GeneticTelepathyActionEvent : EntityTargetActionEvent;

/// <summary>Чтение мыслей (mindreader): узнать истинное имя и должность цели.</summary>
public sealed partial class GeneticMindReadActionEvent : EntityTargetActionEvent;

/// <summary>Сверхчутьё (olfaction): по запаху предмета найти направление к его владельцу.</summary>
public sealed partial class GeneticOlfactionActionEvent : EntityTargetActionEvent;

/// <summary>Создать предмет в руке (геладикинез — снежок, циндикинез — пепел).</summary>
public sealed partial class GeneticSpawnInHandActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public EntProtoId Prototype;
}

/// <summary>Автотомия: вырваться из наручников ценой раны.</summary>
public sealed partial class GeneticAutotomyActionEvent : InstantActionEvent;

/// <summary>Прилив адреналина: восстановить выносливость и ускориться.</summary>
public sealed partial class GeneticAdrenalineActionEvent : InstantActionEvent;

/// <summary>Дальнозоркость: отдалить обзор.</summary>
public sealed partial class GeneticFarsightActionEvent : InstantActionEvent;

/// <summary>Призыв пустоты (void magnet): на 10 секунд исчезнуть из реальности, став неуязвимым.</summary>
public sealed partial class GeneticVoidActionEvent : InstantActionEvent;

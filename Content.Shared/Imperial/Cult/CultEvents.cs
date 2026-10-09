using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cult;

// ───── Действия культиста (actions_cult.dmi) ─────

/// <summary>Communion.</summary>
public sealed partial class CultCommunionActionEvent : InstantActionEvent;

/// <summary>Prepare Blood Magic.</summary>
public sealed partial class CultPrepareBloodMagicActionEvent : InstantActionEvent;

/// <summary>Подготовленное заклинание крови (тип хранится в компоненте действия).</summary>
public sealed partial class CultBloodSpellActionEvent : InstantActionEvent;

/// <summary>Hallucinations: цель-человек в радиусе 7.</summary>
public sealed partial class CultHallucinationsActionEvent : EntityTargetActionEvent;

// ───── Мастер культа ─────

public sealed partial class CultPassMantleActionEvent : InstantActionEvent;
public sealed partial class CultFinalReckoningActionEvent : InstantActionEvent;
public sealed partial class CultMarkTargetActionEvent : WorldTargetActionEvent;
public sealed partial class CultEldritchPulseActionEvent : WorldTargetActionEvent;

// ───── Тёмный дух (руна Царства духов) ─────

public sealed partial class CultSpiritCommunionActionEvent : InstantActionEvent;
public sealed partial class CultGhostMarkActionEvent : InstantActionEvent;

// ───── Предметы ─────

/// <summary>Bloody Bond: вернуть кровавую алебарду.</summary>
public sealed partial class CultBloodBondActionEvent : InstantActionEvent;

// ───── Конструкты ─────

/// <summary>Gauntlet Echo (джаггернаут).</summary>
public sealed partial class CultGauntletEchoActionEvent : WorldTargetActionEvent;

/// <summary>Shield (forcewall/cult).</summary>
public sealed partial class CultForcewallActionEvent : InstantActionEvent;

/// <summary>Phase Shift (призрак).</summary>
public sealed partial class CultPhaseShiftActionEvent : InstantActionEvent;

/// <summary>Lesser Magic Missile (ремесленник).</summary>
public sealed partial class CultMagicMissileActionEvent : InstantActionEvent;

/// <summary>Призыв предмета/турфа под собой (оболочка, пол, стена, камень душ).</summary>
public sealed partial class CultConjureActionEvent : InstantActionEvent
{
    [DataField]
    public EntProtoId? Prototype;

    /// <summary>Поставить пол культа вместо сущности.</summary>
    [DataField]
    public bool Floor;

    /// <summary>Поставить стену культа.</summary>
    [DataField]
    public bool Wall;
}

/// <summary>Summon Rune (create_rune): руна за 6 секунд.</summary>
public sealed partial class CultCreateRuneActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public EntProtoId Rune;

    /// <summary>Номер узора rune_spawn (rune1..rune7).</summary>
    [DataField]
    public int Pattern = 1;
}

/// <summary>Area Conversion (жнец).</summary>
public sealed partial class CultAreaConversionActionEvent : InstantActionEvent;

// ───── DoAfter ─────

[Serializable, NetSerializable]
public sealed partial class CultScribeRuneDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public string Rune = string.Empty;

    [DataField]
    public string? Keyword;
}

[Serializable, NetSerializable]
public sealed partial class CultEraseRuneDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CultCarveSpellDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public CultSpell Spell;

    [DataField]
    public bool OnRune;
}

[Serializable, NetSerializable]
public sealed partial class CultShackleDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CultConstructionDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CultReckoningDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public int Stage;
}

[Serializable, NetSerializable]
public sealed partial class CultBloodBeamDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public bool Firing;
}

[Serializable, NetSerializable]
public sealed partial class CultCreateRuneDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public string Rune = string.Empty;

    [DataField]
    public string? Keyword;
}

[Serializable, NetSerializable]
public sealed partial class CultRunedMetalDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public string Result = string.Empty;

    [DataField]
    public int Cost;
}

[Serializable, NetSerializable]
public sealed partial class CultExorcismDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Seek your Master (конструкты).</summary>
public sealed partial class CultSeekMasterActionEvent : InstantActionEvent;

/// <summary>Seek the Harvest (жнец).</summary>
public sealed partial class CultSeekPreyActionEvent : InstantActionEvent;

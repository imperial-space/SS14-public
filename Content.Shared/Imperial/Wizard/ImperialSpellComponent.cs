using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

/// <summary>INVOCATION_NONE / WHISPER / SHOUT / EMOTE.</summary>
[Serializable, NetSerializable]
public enum ImperialInvocationType : byte
{
    None,
    Whisper,
    Shout,
    Emote,
}

/// <summary>spell_requirements: флаги SPELL_REQUIRES_*.</summary>
[Flags, Serializable, NetSerializable]
public enum ImperialSpellRequirements : ushort
{
    None = 0,
    WizardGarb = 1 << 0,
    NoAntimagic = 1 << 1,
    Station = 1 << 2,
    Human = 1 << 3,
    Mind = 1 << 4,
    MimeVow = 1 << 5,
    CastableAsBrain = 1 << 6,
    CastableWithoutInvocation = 1 << 7,
}

/// <summary>
/// /datum/action/cooldown/spell: заклинание мага. Висит на сущности-действии; откат — UseDelay действия,
/// уровни (spell_level) сокращают его на <see cref="CooldownReductionPerRank"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialSpellComponent : Component
{
    /// <summary>invocation: что произносится при касте.</summary>
    [DataField, AutoNetworkedField]
    public LocId? Invocation;

    /// <summary>invocation_self_message для эмоций.</summary>
    [DataField]
    public LocId? InvocationSelf;

    [DataField, AutoNetworkedField]
    public ImperialInvocationType InvocationType = ImperialInvocationType.None;

    /// <summary>garbled_invocation_prob.</summary>
    [DataField]
    public float GarbledProb = 0.5f;

    [DataField, AutoNetworkedField]
    public ImperialSpellRequirements Requirements = ImperialSpellRequirements.WizardGarb | ImperialSpellRequirements.NoAntimagic;

    // Антимагия (antimagic_flags) — ImperialMagicActionComponent на том же действии.

    /// <summary>school: школа магии (для сект священника).</summary>
    [DataField]
    public string School = "unset";

    [DataField, AutoNetworkedField]
    public int Level = 1;

    [DataField, AutoNetworkedField]
    public int MaxLevel = 5;

    /// <summary>cooldown_reduction_per_rank.</summary>
    [DataField]
    public TimeSpan CooldownReductionPerRank = TimeSpan.Zero;

    /// <summary>Исходный откат (initial(cooldown_time)).</summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(10);

    /// <summary>sound: звук при касте.</summary>
    [DataField]
    public SoundSpecifier? CastSound;

    /// <summary>sparks_amt.</summary>
    [DataField]
    public int Sparks;

    /// <summary>smoke_amt (радиус дыма после каста).</summary>
    [DataField]
    public int Smoke;

    /// <summary>SPELL_NO_IMMEDIATE_COOLDOWN: эффект сам решает, когда начать откат.</summary>
    [ViewVariables]
    public bool SkipCooldown;

    /// <summary>SPELL_NO_FEEDBACK: без произнесения и звука.</summary>
    [ViewVariables]
    public bool SkipFeedback;

    /// <summary>initial(name): имя без приставки уровня.</summary>
    [DataField, AutoNetworkedField]
    public string? BaseName;
}

/// <summary>Попытка каста: другие системы могут отменить (COMSIG_SPELL_CAN_CAST_CHECK / COMSIG_MOB_BEFORE_SPELL_CAST).</summary>
[ByRefEvent]
public record struct ImperialSpellCastAttemptEvent(EntityUid Caster, EntityUid Spell, bool Cancelled = false);

/// <summary>Заклинание прочитано (COMSIG_MOB_CAST_SPELL): поднимается на заклинателе.</summary>
[ByRefEvent]
public readonly record struct ImperialSpellCastEvent(EntityUid Caster, EntityUid Spell);

/// <summary>COMSIG_MOB_PRE_INVOCATION: можно подменить фразу и тип произнесения.</summary>
[ByRefEvent]
public record struct ImperialSpellInvocationEvent(EntityUid Spell, string? Message, ImperialInvocationType Type);

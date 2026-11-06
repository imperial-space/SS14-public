using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

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

    [DataField]
    public TimeSpan GetCooldownDelay = TimeSpan.FromSeconds(0.25);
}

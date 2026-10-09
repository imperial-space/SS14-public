using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Cult.Components;

/// <summary>
/// Руна культа (obj/effect/rune). Значения — из runes.dm.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CultRuneComponent : Component
{
    [DataField(required: true)]
    public CultRuneType RuneType;

    /// <summary>cultist_name.</summary>
    [DataField(required: true)]
    public LocId CultistName;

    /// <summary>cultist_desc.</summary>
    [DataField(required: true)]
    public LocId CultistDesc;

    /// <summary>invocation: произносится каждым призывающим.</summary>
    [DataField]
    public string Invocation = "Aiy ele-mayo!";

    /// <summary>req_cultists.</summary>
    [DataField]
    public int ReqCultists = 1;

    /// <summary>req_cultists_text.</summary>
    [DataField]
    public LocId? ReqCultistsText;

    /// <summary>invoke_damage (brute каждому призывающему).</summary>
    [DataField]
    public float InvokeDamage;

    /// <summary>construct_invoke.</summary>
    [DataField]
    public bool ConstructInvoke = true;

    /// <summary>req_keyword.</summary>
    [DataField]
    public bool ReqKeyword;

    /// <summary>can_be_scribed.</summary>
    [DataField]
    public bool CanBeScribed = true;

    /// <summary>scribe_delay.</summary>
    [DataField]
    public TimeSpan ScribeDelay = TimeSpan.FromSeconds(4);

    /// <summary>scribe_damage.</summary>
    [DataField]
    public float ScribeDamage = 0.1f;

    /// <summary>erase_time.</summary>
    [DataField]
    public TimeSpan EraseTime = TimeSpan.FromSeconds(1.5);

    /// <summary>no_scribe_boost: не ускоряется на полу культа.</summary>
    [DataField]
    public bool NoScribeBoost;

    /// <summary>log_when_erased: спросить подтверждение при стирании.</summary>
    [DataField]
    public bool LogWhenErased;

    /// <summary>Подсказка призыва, показанная культистам при черчении.</summary>
    [DataField]
    public int ScribeOrder;

    /// <summary>Glow/fade эффект при активации (do_invoke_glow).</summary>
    [DataField]
    public bool InvokeGlow = true;

    [DataField]
    public SoundSpecifier? InvokeSound;

    /// <summary>color руны (RUNE_COLOR_*).</summary>
    [DataField]
    public Color RuneColor = Color.Red;

    /// <summary>Номер узора rune_spawn (rune1..rune7) для создания конструктами.</summary>
    [DataField]
    public int SpawnPattern = 1;

    /// <summary>Совместимость с нуль-жезлом: эффект разрушения руны.</summary>
    [DataField]
    public string? DestructionState = "destroyed";

    [ViewVariables] public string? Keyword;
    /// <summary>listkey руны телепортации: «ключ отсек».</summary>
    [ViewVariables] public string? ListKey;
    [ViewVariables] public bool InUse;
    [ViewVariables] public bool Concealed;
    [ViewVariables] public int GlowCounter;
    [ViewVariables] public int FailCounter;

    /// <summary>Барьер руны барьера.</summary>
    [ViewVariables] public EntityUid? Barrier;

    /// <summary>Сколько духов поддерживает руна Царства духов.</summary>
    [ViewVariables] public int Ghosts;

    /// <summary>Руна Нар'Си уже использована.</summary>
    [ViewVariables] public bool Used;

    /// <summary>Портал на руне телепортации после прибытия из космоса/лаваленда.</summary>
    [ViewVariables] public EntityUid? Portal;
    [ViewVariables] public string? PortalDescription;
}

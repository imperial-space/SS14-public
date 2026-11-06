using System.Linq;
using System.Text;
using Content.Server.Chat.Managers;
using Content.Shared.Alert;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Mood;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Drunk;
using Content.Shared.StatusEffectNew;
using Content.Shared.Traits.Assorted;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Mood;

/// <summary>/datum/mood: мудлеты, настроение и рассудок существа.</summary>
[RegisterComponent]
public sealed partial class ImperialMoodComponent : Component
{
    [ViewVariables]
    public Dictionary<string, ImperialMoodActive> Events = new();

    /// <summary>mood: сумма мудлетов.</summary>
    [ViewVariables]
    public int Mood;

    /// <summary>shown_mood: сумма видимых мудлетов.</summary>
    [ViewVariables]
    public int ShownMood;

    /// <summary>mood_level 1–9.</summary>
    [ViewVariables]
    public int MoodLevel = ImperialMood.LevelNeutral;

    /// <summary>sanity 0–150.</summary>
    [DataField]
    public float Sanity = ImperialMood.SanityNeutral;

    /// <summary>sanity_level 1–6.</summary>
    [ViewVariables]
    public int SanityLevel = 2;

    /// <summary>insanity_effect: на сколько раньше наступает критическое состояние.</summary>
    [ViewVariables]
    public int InsanityEffect;

    [DataField]
    public float MoodModifier = 1f;

    [DataField]
    public float PositiveModifier = 1f;

    [DataField]
    public float NegativeModifier = 1f;

    [DataField]
    public float PositiveLengthModifier = 1f;

    [DataField]
    public float NegativeLengthModifier = 1f;

    [ViewVariables]
    public bool Hallucinating;
    [ViewVariables]
    public TimeSpan NextTick;

    /// <summary>Каждые 5 секунд: окружение, личности, роли.</summary>
    [ViewVariables]
    public TimeSpan NextSlowTick;

    /// <summary>personalities: savefile_key личностей.</summary>
    [ViewVariables]
    public HashSet<string> Personalities = new();

    /// <summary>Стадия опьянения, по которой выставлен мудлет drunk.</summary>
    [ViewVariables]
    public int DrunkStage;

    [DataField]
    public TimeSpan TickDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan SlowTickDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan IngestingDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public float CheckLikedChance = 0.5f;

    [DataField]
    public float CheckLikedChance2 = 0.2f;

    [DataField]
    public float UpdateErraticChance = 0.01f;
}

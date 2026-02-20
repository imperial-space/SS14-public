using Content.Shared.Prototypes;
using Robust.Shared.Audio;

namespace Content.Server.Imperial.Sanity.Components;

[RegisterComponent]
public sealed partial class SanityComponent : Component
{
    [DataField("startSanity")]
    public float StartSanity = 50f;

    [DataField("minSanity")]
    public float MinSanity = 1f;

    [DataField("maxSanity")]
    public float MaxSanity = 100f;

    [DataField("highThreshold")]
    public float HighThreshold = 80f;

    [DataField("lowThreshold")]
    public float LowThreshold = 10f;

    [ViewVariables(VVAccess.ReadOnly)]
    public float Value = 50f;

    [DataField("updateInterval")]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    [DataField("proximityInterval")]
    public TimeSpan ProximityInterval = TimeSpan.FromSeconds(2);

    [DataField("silenceTimeout")]
    public TimeSpan SilenceTimeout = TimeSpan.FromSeconds(90);

    [DataField("silencePenaltyInterval")]
    public TimeSpan SilencePenaltyInterval = TimeSpan.FromSeconds(10);

    [DataField("coffeeGainCooldown")]
    public TimeSpan CoffeeGainCooldown = TimeSpan.FromSeconds(20);

    [DataField("initialLowSoundDelay")]
    public TimeSpan InitialLowSoundDelay = TimeSpan.FromSeconds(8);

    [DataField("initialHighRegenDelay")]
    public TimeSpan InitialHighRegenDelay = TimeSpan.FromSeconds(1);

    [DataField("highWalkMultiplier")]
    public float HighWalkMultiplier = 1.05f;

    [DataField("highSprintMultiplier")]
    public float HighSprintMultiplier = 1.05f;

    [DataField("lowWalkMultiplier")]
    public float LowWalkMultiplier = 0.88f;

    [DataField("lowSprintMultiplier")]
    public float LowSprintMultiplier = 0.88f;

    [DataField("highHungerDecayMultiplier")]
    public float HighHungerDecayMultiplier = 0.92f;

    [DataField("highThirstDecayMultiplier")]
    public float HighThirstDecayMultiplier = 0.92f;

    [DataField("highBloodUpdateMultiplier")]
    public float HighBloodUpdateMultiplier = 0.90f;

    [DataField("lowHungerDecayMultiplier")]
    public float LowHungerDecayMultiplier = 1.25f;

    [DataField("lowThirstDecayMultiplier")]
    public float LowThirstDecayMultiplier = 1.25f;

    [DataField("lowBloodUpdateMultiplier")]
    public float LowBloodUpdateMultiplier = 1.20f;

    [DataField("damageLossPerPoint")]
    public float DamageLossPerPoint = 0.08f;

    [DataField("damageLossCap")]
    public float DamageLossCap = 5f;

    [DataField("dialogueGain")]
    public float DialogueGain = 0.6f;

    [DataField("eatGain")]
    public float EatGain = 1.2f;

    [DataField("drinkGain")]
    public float DrinkGain = 0.8f;

    [DataField("coffeeGain")]
    public float CoffeeGain = 1.5f;

    [DataField("nearHumanGain")]
    public float NearHumanGain = 0.35f;

    [DataField("corpseLoss")]
    public float CorpseLoss = 0.9f;

    [DataField("nearNdaGain")]
    public float NearNdaGain = 1.0f;

    [DataField("highHungerPerTick")]
    public float HighHungerPerTick = 0.01f;

    [DataField("highThirstPerTick")]
    public float HighThirstPerTick = 0.06f;

    [DataField("highBloodPerTick")]
    public float HighBloodPerTick = 0.03f;

    [DataField("lowHungerPerTick")]
    public float LowHungerPerTick = -0.12f;

    [DataField("lowThirstPerTick")]
    public float LowThirstPerTick = -0.26f;

    [DataField("lowBloodPerTick")]
    public float LowBloodPerTick = -0.02f;

    [DataField("silenceLoss")]
    public float SilenceLoss = 1.0f;

    [DataField("highRegenPerType")]
    public float HighRegenPerType = 0.03f;

    [DataField("nearHumanRadius")]
    public float NearHumanRadius = 4f;

    [DataField("corpseRadius")]
    public float CorpseRadius = 8f;

    [DataField("ndaRadius")]
    public float NdaRadius = 8f;

    [DataField("scpProximityRadius")]
    public float ScpProximityRadius = 8f;

    [DataField("scpFriendlyGain")]
    public float ScpFriendlyGain = 0.5f;

    [DataField("scpHostileLoss")]
    public float ScpHostileLoss = 1f;

    [DataField("seenScpLoss")]
    public float SeenScpLoss = 1f;

    [DataField("lowSoundMinInterval")]
    public TimeSpan LowSoundMinInterval = TimeSpan.FromSeconds(12);

    [DataField("lowSoundMaxInterval")]
    public TimeSpan LowSoundMaxInterval = TimeSpan.FromSeconds(25);

    [DataField("lowSanitySounds")]
    public List<SoundSpecifier> LowSanitySounds =
    [
        new SoundPathSpecifier("/Audio/Imperial/Seriozha/Fredik21/reason/breath.ogg"),
        new SoundPathSpecifier("/Audio/Imperial/Seriozha/Fredik21/reason/heart.ogg")
    ];

    [ViewVariables]
    public TimeSpan NextUpdate;

    [ViewVariables]
    public TimeSpan NextProximityCheck;

    [ViewVariables]
    public TimeSpan LastDialogueTime;

    [ViewVariables]
    public TimeSpan NextSilencePenalty;

    [ViewVariables]
    public TimeSpan NextCoffeeGain;

    [ViewVariables]
    public TimeSpan NextLowSound;

    [ViewVariables]
    public TimeSpan NextHighRegenTick;

    [ViewVariables]
    public float LastHunger;

    [ViewVariables]
    public float LastThirst;

    [ViewVariables]
    public SanityState State = SanityState.Normal;
}

public enum SanityState : byte
{
    Normal,
    High,
    Low,
}

using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Content.Shared.FixedPoint;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>slimecross/stabilized: пассивный эффект для носителя.</summary>
[RegisterComponent]
public sealed partial class SlimeStabilizedComponent : Component
{
    [ViewVariables]
    public EntityUid? Holder;

    [ViewVariables]
    public TimeSpan NextTick;

    [ViewVariables]
    public int Cooldown;

    /// <summary>gold: тип фамильяра.</summary>
    [ViewVariables]
    public EntProtoId? FamiliarProto;

    [ViewVariables]
    public string FamiliarName = string.Empty;

    [ViewVariables]
    public EntityUid? Familiar;

    /// <summary>cerulean: клон владельца.</summary>
    [ViewVariables]
    public EntityUid? Clone;

    /// <summary>rainbow: вложенное регенеративное ядро.</summary>
    [ViewVariables]
    public EntityUid? RegenCore;

    /// <summary>bluespace: здоровье на прошлом тике.</summary>
    [ViewVariables]
    public float? LastHealth;

    /// <summary>bluespace: откат телепорта.</summary>
    [ViewVariables]
    public TimeSpan BluespaceCooldown;

    /// <summary>sepia: текущий шаг скорости.</summary>
    [ViewVariables]
    public int SepiaMod;

    /// <summary>green: исходное имя.</summary>
    [ViewVariables]
    public string? OriginalName;

    /// <summary>pink: мир нарушен до этого времени.</summary>
    [ViewVariables]
    public TimeSpan BrokenPeaceUntil;

    [ViewVariables]
    public Dictionary<EntityUid, float> PinkTracked = new();

    [ViewVariables]
    public bool PinkPeaceful;

    [DataField]
    public TimeSpan BluespaceCooldown2 = TimeSpan.FromMinutes(2);

    [DataField]
    public TimeSpan BrokenPeaceDelay = TimeSpan.FromMinutes(2);

    [DataField]
    public TimeSpan TickDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public float TickStabilizedSlimeGreyRange = 1.5f;

    [DataField]
    public float TickStabilizedSlimeDarkBlueChance = 0.8f;

    [DataField]
    public FixedPoint2 TickStabilizedSlimeDarkBlueQuantity = 5;

    [DataField]
    public float TickStabilizedSlimeSepiaChance = 0.5f;

    [DataField]
    public float TickStabilizedSlimeOilTotalIntensity = 140f;

    [DataField]
    public float TickStabilizedSlimeOilSlope = 10f;

    [DataField]
    public float TickStabilizedSlimeOilMaxTileIntensity = 30f;

    [DataField]
    public float TickStabilizedSlimeLightPinkRange = 1.5f;

    [DataField]
    public float TickStabilizedSlimeLightPinkAmount = 5f;

    [DataField]
    public int TickStabilizedBluespaceRange = 15;

    [DataField]
    public float TickStabilizedPinkRange = 7f;
}

using Robust.Shared.Map;
using Robust.Shared.Prototypes;

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
}

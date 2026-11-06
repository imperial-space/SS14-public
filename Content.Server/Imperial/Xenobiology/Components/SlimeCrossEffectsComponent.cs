using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>Временные эффекты печенья, ядер и экстрактов на существе.</summary>
[RegisterComponent]
public sealed partial class SlimeCrossEffectsComponent : Component
{
    [ViewVariables]
    public Dictionary<string, TimeSpan> Effects = new();

    [ViewVariables]
    public TimeSpan NextTick;

    /// <summary>Пацифизм выдан эффектами (а не другим источником).</summary>
    [ViewVariables]
    public bool GavePacified;

    /// <summary>Неуязвимость выдана эффектами.</summary>
    [ViewVariables]
    public bool GaveGodmode;

    /// <summary>Нескользящие ботинки выданы эффектами.</summary>
    [ViewVariables]
    public bool GaveNoSlip;

    [DataField]
    public TimeSpan TickEffectsDuration = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan TickDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public float TickEffectsRange = 1.5f;

    [DataField]
    public float TickEffectsChance = 0.5f;

    [DataField]
    public float TickEffectsAmount = 2f;
}

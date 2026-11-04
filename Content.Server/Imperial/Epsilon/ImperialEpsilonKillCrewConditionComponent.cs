using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Objectives.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Epsilon;

/// <summary>Цель «уничтожить весь экипаж»: в описании счётчик оставшихся живых.</summary>
[RegisterComponent]
public sealed partial class ImperialEpsilonKillCrewConditionComponent : Component
{
    /// <summary>Наибольшее число живого экипажа за время цели — от него считается прогресс.</summary>
    [ViewVariables]
    public int MaxCrew;

    [ViewVariables]
    public int LastRemaining = -1;
}

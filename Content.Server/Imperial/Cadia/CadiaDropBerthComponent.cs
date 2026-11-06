using System.Numerics;
using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Shared.DoAfter;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>Пусковая шахта когтистой капсулы на десантной палубе флагмана.</summary>
[RegisterComponent]
public sealed partial class CadiaDropBerthComponent : Component
{
    /// <summary>Капсула возвращается на корабль не сразу.</summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(25);

    /// <summary>Кто стоит в этом радиусе от шахты, тот и летит.</summary>
    [DataField]
    public float Radius = 1.5f;

    /// <summary>Мест в капсуле.</summary>
    [DataField]
    public int Capacity = 6;

    [ViewVariables]
    public TimeSpan NextLaunch;

    /// <summary>Сколько урона шахта выдерживает, прежде чем разбиться.</summary>
    [DataField]
    public float BreakAt = 120f;

    /// <summary>Кто умеет чинить разбитую шахту.</summary>
    [DataField]
    public List<string> Repairers = new() { "CadiaHeretek" };

    [DataField]
    public TimeSpan RepairTime = TimeSpan.FromSeconds(12);

    /// <summary>Шахта разбита: капсулы не стартуют, над ней горит пожар.</summary>
    [ViewVariables]
    public bool Broken;

    [ViewVariables]
    public EntityUid? Fire;
}

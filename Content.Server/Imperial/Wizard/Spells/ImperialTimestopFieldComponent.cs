using System.Linq;
using System.Numerics;
using Content.Server.Emp;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Throwing;
using Content.Shared.Temperature.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Wizard.Spells;

/// <summary>Поле остановки времени: всё внутри, кроме неуязвимых, замирает.</summary>
[RegisterComponent]
public sealed partial class ImperialTimestopFieldComponent : Component
{
    [ViewVariables]
    public float Range = 2f;

    [ViewVariables]
    public TimeSpan End;

    [ViewVariables]
    public HashSet<EntityUid> Immune = new();

    /// <summary>Замороженные снаряды и их скорости.</summary>
    [ViewVariables]
    public Dictionary<EntityUid, Vector2> Projectiles = new();

    [ViewVariables]
    public HashSet<EntityUid> Mobs = new();
}

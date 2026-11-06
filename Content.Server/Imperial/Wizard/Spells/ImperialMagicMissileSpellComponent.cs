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

/// <summary>aoe/magic_missile: медленные самонаводящиеся ракеты во всех вокруг.</summary>
[RegisterComponent]
public sealed partial class ImperialMagicMissileSpellComponent : Component
{
    [DataField]
    public EntProtoId Projectile = "ImperialProjectileMagicMissile";

    [DataField]
    public float Radius = 7f;

    /// <summary>max_targets (0 — без ограничения).</summary>
    [DataField]
    public int MaxTargets;
}

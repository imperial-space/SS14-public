using System.Numerics;
using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Paths.Cosmos;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Heretic.Paths.Cosmos;

/// <summary>Маркер для снарядов Star Gazer, чтобы не конфликтовать с HereticStarBlastProjectileComponent.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class HereticStarGazerProjectileComponent : Component
{
    [DataField] public EntityUid? Shooter;
    [DataField] [ViewVariables, AutoPausedField]
 public TimeSpan NextTrail;
    [DataField] public float TrailInterval = 0.3f;
}

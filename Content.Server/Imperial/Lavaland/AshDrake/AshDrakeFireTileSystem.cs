using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.AshDrake;

/// <summary>Огненные клетки способностей дракона у игрока (кровь дракона): урон стоящим игрокам раз в 0.15 с.</summary>
public sealed class AshDrakeFireTileSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AshDrakeFireTileComponent, TransformComponent>();
        while (query.MoveNext(out _, out var fireTile, out var xform))
        {
            if (_timing.CurTime < fireTile.NextDamageTime)
                continue;
            fireTile.NextDamageTime = _timing.CurTime + TimeSpan.FromSeconds(0.15);

            if (!Exists(fireTile.DrakeUid))
                continue;

            var spec = new DamageSpecifier();
            spec.DamageDict.Add("Heat", FixedPoint2.New(fireTile.Damage));

            foreach (var session in _playerManager.Sessions)
            {
                if (session.Status != SessionStatus.InGame || session.AttachedEntity is not { Valid: true } candidate)
                    continue;
                if (!TryComp<MobStateComponent>(candidate, out var state) || state.CurrentState != MobState.Alive)
                    continue;
                if (!TryComp<DamageableComponent>(candidate, out var damageable))
                    continue;

                var coords = Transform(candidate).Coordinates;
                if (coords.EntityId != xform.Coordinates.EntityId ||
                    MathF.Round(coords.X) != MathF.Round(xform.Coordinates.X) ||
                    MathF.Round(coords.Y) != MathF.Round(xform.Coordinates.Y))
                    continue;

                _damageable.TryChangeDamage((candidate, damageable), spec, origin: fireTile.DrakeUid);
            }
        }
    }
}

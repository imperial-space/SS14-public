using Content.Server.Imperial.TerrorSpider.Components;
using Content.Shared.Imperial.TerrorSpider.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Server.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.TerrorSpider.Systems;

public sealed class TerrorSpiderGuardianLeashSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> TerrorSpiderTag = "TerrorSpider";

    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<TerrorSpiderGuardianLeashComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var leash, out var mobState))
        {
            if (mobState.CurrentState == MobState.Dead)
                continue;

            if (now < leash.NextTickTime)
                continue;

            leash.NextTickTime = now + TimeSpan.FromSeconds(leash.TickInterval);

            var guardPos = Transform(uid).MapPosition;
            var nearRoyal = IsNearAliveRoyal(guardPos, leash.RequiredRoyalRange, uid);

            if (nearRoyal)
                continue;

            var damage = new DamageSpecifier();
            damage.DamageDict["Blunt"] = FixedPoint2.New(leash.DamageIfFar);
            _damageable.TryChangeDamage(uid, damage, ignoreResistances: false, interruptsDoAfters: false);

            if (now < leash.NextWarningTime)
                continue;

            leash.NextWarningTime = now + TimeSpan.FromSeconds(leash.WarningCooldown);

            _popup.PopupEntity(
                "Вы слишком далеко! Вернитесь к принцессе или королеве.",
                uid,
                uid,
                PopupType.SmallCaution);
        }
    }

    private bool IsNearAliveRoyal(MapCoordinates guardPos, float maxRange, EntityUid guard)
    {
        var maxRangeSquared = maxRange * maxRange;

        var princessQuery = EntityQueryEnumerator<TerrorSpiderPrincessComponent, MobStateComponent, TransformComponent>();
        while (princessQuery.MoveNext(out var uid, out _, out var state, out var xform))
        {
            if (uid == guard || state.CurrentState != MobState.Alive)
                continue;

            if (xform.MapPosition.MapId != guardPos.MapId)
                continue;

            if ((xform.MapPosition.Position - guardPos.Position).LengthSquared() <= maxRangeSquared)
                return true;
        }

        var queenQuery = EntityQueryEnumerator<TerrorSpiderQueenComponent, MobStateComponent, TransformComponent>();
        while (queenQuery.MoveNext(out var uid, out _, out var state, out var xform))
        {
            if (uid == guard || state.CurrentState != MobState.Alive)
                continue;

            if (xform.MapPosition.MapId != guardPos.MapId)
                continue;

            if ((xform.MapPosition.Position - guardPos.Position).LengthSquared() <= maxRangeSquared)
                return true;
        }

        return false;
    }

    private bool IsTerrorSpider(EntityUid uid)
    {
        return HasComp<TerrorSpiderWebBuffReceiverComponent>(uid) || _tag.HasTag(uid, TerrorSpiderTag);
    }
}

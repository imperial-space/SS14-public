using Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Systems;

/// <summary>
/// Статусы трофеев дробителя, влияющие на движение: ice_block_talisman (заморозка) и wolf_ear (ускорение).
/// </summary>
public sealed class CrusherStatusSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CrusherFrozenComponent, RefreshMovementSpeedModifiersEvent>(OnFrozenSpeed);
        SubscribeLocalEvent<CrusherSpeedBoostComponent, RefreshMovementSpeedModifiersEvent>(OnBoostSpeed);
    }

    private void OnFrozenSpeed(Entity<CrusherFrozenComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.LifeStage < ComponentLifeStage.Stopping)
            args.ModifySpeed(0f, 0f);
    }

    private void OnBoostSpeed(Entity<CrusherSpeedBoostComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.LifeStage < ComponentLifeStage.Stopping)
            args.ModifySpeed(ent.Comp.Modifier, ent.Comp.Modifier);
    }

    public void Freeze(EntityUid uid, TimeSpan duration)
    {
        var comp = EnsureComp<CrusherFrozenComponent>(uid);
        comp.Until = _timing.CurTime + duration;
        Dirty(uid, comp);
        _movement.RefreshMovementSpeedModifiers(uid);
    }

    public void SpeedBoost(EntityUid uid, TimeSpan duration)
    {
        var comp = EnsureComp<CrusherSpeedBoostComponent>(uid);
        comp.Until = _timing.CurTime + duration;
        Dirty(uid, comp);
        _movement.RefreshMovementSpeedModifiers(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        _expired.Clear();

        var frozen = EntityQueryEnumerator<CrusherFrozenComponent>();
        while (frozen.MoveNext(out var uid, out var comp))
        {
            if (now >= comp.Until)
                _expired.Add((uid, typeof(CrusherFrozenComponent)));
        }

        var boost = EntityQueryEnumerator<CrusherSpeedBoostComponent>();
        while (boost.MoveNext(out var uid, out var comp))
        {
            if (now >= comp.Until)
                _expired.Add((uid, typeof(CrusherSpeedBoostComponent)));
        }

        foreach (var (uid, type) in _expired)
        {
            RemComp(uid, type);
            _movement.RefreshMovementSpeedModifiers(uid);
        }
    }

    private readonly List<(EntityUid, Type)> _expired = new();
}

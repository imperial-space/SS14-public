using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.Damage.Systems;
using Content.Shared.Ghost;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC;
using Robust.Server.Player;
using Robust.Shared.Enums;

namespace Content.Server.Imperial.Lavaland.MegafaunaSleep;

/// <summary>
/// Сон мегафауны: пока босс спит, у него нулевая скорость и выключен ИИ (HTN), а системы боссов
/// пропускают способности. Первый агр — игрок подошёл близко или ударил; дальше босс преследует цель.
/// </summary>
public sealed class LavalandMegafaunaSleepSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeedModifier = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandMegafaunaSleepComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<LavalandMegafaunaSleepComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<LavalandMegafaunaSleepComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnStartup(EntityUid uid, LavalandMegafaunaSleepComponent comp, ComponentStartup args)
    {
        _movementSpeedModifier.RefreshMovementSpeedModifiers(uid);
        if (TryComp<HTNComponent>(uid, out var htn))
            _npc.SleepNPC(uid, htn);
    }

    private void OnRefreshSpeed(EntityUid uid, LavalandMegafaunaSleepComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(0f, 0f);
    }

    /// <summary>Атака будит босса, даже если игрок стреляет издалека.</summary>
    private void OnDamageChanged(EntityUid uid, LavalandMegafaunaSleepComponent comp, DamageChangedEvent args)
    {
        if (comp.WakeOnDamage && args.DamageIncreased)
            Wake(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var toWake = new List<EntityUid>();

        var query = EntityQueryEnumerator<LavalandMegafaunaSleepComponent, TransformComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var sleep, out var xform, out var mobState))
        {
            if (mobState.CurrentState != MobState.Alive)
            {
                toWake.Add(uid);
                continue;
            }

            // NPCSystem может разбудить ИИ сам (например, при инициализации карты) — держим спящим.
            if (HasComp<ActiveNPCComponent>(uid) && TryComp<HTNComponent>(uid, out var htn))
                _npc.SleepNPC(uid, htn);

            var position = _transform.GetWorldPosition(xform);
            foreach (var session in _playerManager.Sessions)
            {
                if (session.Status != SessionStatus.InGame
                    || session.AttachedEntity is not { Valid: true } playerEnt)
                    continue;

                if (HasComp<GhostComponent>(playerEnt) || !_mobState.IsAlive(playerEnt))
                    continue;

                var playerXform = Transform(playerEnt);
                if (playerXform.MapUid != xform.MapUid)
                    continue;

                if ((position - _transform.GetWorldPosition(playerXform)).Length() <= sleep.WakeRadius)
                {
                    toWake.Add(uid);
                    break;
                }
            }
        }

        foreach (var uid in toWake)
        {
            Wake(uid);
        }
    }

    private void Wake(EntityUid uid)
    {
        if (!RemComp<LavalandMegafaunaSleepComponent>(uid))
            return;

        _movementSpeedModifier.RefreshMovementSpeedModifiers(uid);
        if (_mobState.IsAlive(uid) && TryComp<HTNComponent>(uid, out var htn))
            _npc.WakeNPC(uid, htn);
    }
}

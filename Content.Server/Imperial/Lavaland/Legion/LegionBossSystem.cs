using System.Numerics;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.Popups;
using Content.Shared.Movement.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Server.Imperial.Lavaland.MegafaunaSleep;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.Legion;

public sealed class LegionBossSystem : EntitySystem
{
    private const string ChargeSound = "/Audio/Imperial/boss/sound_weapons_sonic_jackhammer.ogg";
    private const float SpinDegreesPerSecond = 540f;

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speedModifier = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LegionBossComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<LegionBossComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    // ── Speed override during charge ────────────────────────────────────────

    private void OnRefreshSpeed(EntityUid uid, LegionBossComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        if (!comp.IsCharging)
            return;

        // Acceleration from 0 to ChargeSpeed over ChargeAccelerationDuration
        var elapsed = _timing.CurTime - comp.ChargeStartTime;
        var progress = MathF.Min((float)elapsed.TotalSeconds / comp.ChargeAccelerationDuration, 1.0f);
        var currentSpeed = comp.NormalSpeed + (comp.ChargeSpeed - comp.NormalSpeed) * progress;

        var mult = currentSpeed / comp.NormalSpeed;
        args.ModifySpeed(mult, mult);
    }

    // ── Update loop ─────────────────────────────────────────────────────────

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<LegionBossComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var comp, out var mobState))
        {
            if (mobState.CurrentState != MobState.Alive)
                continue;

            // Charge mode: count down; HTN + speed multiplier handle movement
            if (comp.IsCharging)
            {
                // Keep speed ramp updated every tick during acceleration window.
                _speedModifier.RefreshMovementSpeedModifiers(uid);

                // Force visible spin while charging.
                var rot = _transform.GetWorldRotation(uid) + Angle.FromDegrees(SpinDegreesPerSecond * frameTime);
                _transform.SetWorldRotation(uid, rot);

                if (_timing.CurTime >= comp.ChargeEndTime)
                    EndCharge(uid, comp);
                continue;
            }

            // Keep default orientation horizontal when not charging.
            _transform.SetWorldRotation(uid, Angle.Zero);

            // Decision cycle
            if (_timing.CurTime >= comp.NextDecisionTime)
            {
                MakeDecision(uid, comp);
                comp.NextDecisionTime = _timing.CurTime + TimeSpan.FromSeconds(comp.DecisionCooldown);
            }
        }
    }

    // ── Decision ────────────────────────────────────────────────────────────

    private void MakeDecision(EntityUid uid, LegionBossComponent comp)
    {
        if (HasComp<LavalandMegafaunaSleepComponent>(uid))
            return;

        if (!TryFindNearbyPlayer(uid, 20f, out _))
            return;

        if (_random.NextFloat() < comp.SummonChance)
            DoSummon(uid, comp);
        else
            StartCharge(uid, comp);
    }

    // ── Ability 1: Skull summon ─────────────────────────────────────────────

    private void DoSummon(EntityUid uid, LegionBossComponent comp)
    {
        var skullCount = 0;
        var query = EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out var skullUid, out var meta))
        {
            if (meta.EntityPrototype?.ID == comp.SummonedPrototype.Id)
                skullCount++;
        }
        if (skullCount >= comp.MaxConcurrentSkulls)
            return;

        if (comp.SummonSound != null)
            _audio.PlayPvs(comp.SummonSound, uid);
        PopupToNearbyPlayers(uid, Loc.GetString("legion-ability-summon"));

        var worldPos = _transform.GetWorldPosition(uid);
        var angle = _random.NextFloat() * MathF.PI * 2f;
        var dist = 0.5f + _random.NextFloat() * comp.SummonRadius;
        var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
        Spawn(comp.SummonedPrototype, new MapCoordinates(worldPos + offset, Transform(uid).MapID));
    }

    // ── Ability 2: Charge mode ───────────────────────────────────────────────

    private void StartCharge(EntityUid uid, LegionBossComponent comp)
    {
        _audio.PlayPvs(ChargeSound, uid);
        PopupToNearbyPlayers(uid, Loc.GetString("legion-ability-charge"));

        // Ensure movement code never overrides our manual spin.
        EnsureComp<NoRotateOnMoveComponent>(uid);

        comp.IsCharging = true;
        comp.ChargeStartTime = _timing.CurTime;
        comp.ChargeEndTime = _timing.CurTime + TimeSpan.FromSeconds(comp.ChargeDuration);
        SetHTNMeleeRange(uid, 1.0f);
        _speedModifier.RefreshMovementSpeedModifiers(uid);
    }

    private void EndCharge(EntityUid uid, LegionBossComponent comp)
    {
        comp.IsCharging = false;
        SetHTNMeleeRange(uid, comp.NormalMeleeRange);
        _speedModifier.RefreshMovementSpeedModifiers(uid);
        comp.NextDecisionTime = _timing.CurTime + TimeSpan.FromSeconds(comp.DecisionCooldown);
    }

    private void SetHTNMeleeRange(EntityUid uid, float range)
    {
        if (!TryComp<HTNComponent>(uid, out var htn))
            return;
        htn.Blackboard.SetValue("MeleeRange", range);
        _htn.Replan(htn);
    }

    // ── Split on death ───────────────────────────────────────────────────────

    private void OnMobStateChanged(EntityUid uid, LegionBossComponent comp, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (comp.SplitPrototype == null || comp.SplitCount <= 0)
            return;

        var coords = Transform(uid).Coordinates;
        for (var i = 0; i < comp.SplitCount; i++)
        {
            var angle = MathF.PI * 2f / comp.SplitCount * i;
            var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 1.2f;
            Spawn(comp.SplitPrototype.Value, coords.Offset(offset));
        }
    }

    // ── Utility ─────────────────────────────────────────────────────────────

    private void PopupToNearbyPlayers(EntityUid source, string message, float range = 20f)
    {
        var sourcePos = _transform.GetWorldPosition(source);
        var sourceMap = Transform(source).MapID;

        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.InGame || session.AttachedEntity is not { Valid: true } player)
                continue;

            if (Transform(player).MapID != sourceMap)
                continue;

            if ((_transform.GetWorldPosition(player) - sourcePos).Length() > range)
                continue;

            _popup.PopupEntity(message, source, player, PopupType.LargeCaution);
        }
    }

    private bool TryFindNearbyPlayer(EntityUid uid, float range, out EntityUid result)
    {
        result = default;
        var ourPos = _transform.GetWorldPosition(uid);
        var ourMap = Transform(uid).MapID;
        var best = float.MaxValue;

        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.InGame)
                continue;
            var playerUid = session.AttachedEntity;
            if (playerUid == null)
                continue;
            if (Transform(playerUid.Value).MapID != ourMap)
                continue;
            var dist = (_transform.GetWorldPosition(playerUid.Value) - ourPos).Length();
            if (dist <= range && dist < best)
            {
                best = dist;
                result = playerUid.Value;
            }
        }

        return result != default;
    }
}

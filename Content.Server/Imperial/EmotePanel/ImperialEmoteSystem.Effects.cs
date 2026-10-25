using Content.Server.Screens.Components;
using Content.Server.Station.Systems;
using Content.Shared.Bed.Sleep;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Drunk;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Imperial.EmotePanel;
using Content.Shared.Mobs.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Standing;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Toggleable;
using Content.Shared.Wagging;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.EmotePanel;

/// <summary>Эффекты run_emote: оглушения, заразительный зевок, хвост, экраны ИИ и анимации у клиентов.</summary>
public sealed partial class ImperialEmoteSystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly SleepingSystem _sleeping = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedStationAiSystem _stationAi = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedDrunkSystem _drunk = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;

    /// <summary>Картинка ИИ на экране из PR станционного ИИ: по имени, чтобы не зависеть от его кода.</summary>
    private const string AiDisplayComponent = "ImperialAiDisplay";

    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";

    /// <summary>YAWN_PROPAGATE_CHANCE_BASE и YAWN_PROPAGATE_CHANCE_DECAY.</summary>
    private const int YawnChanceBase = 20;
    private const int YawnChanceDecay = 4;

    /// <summary>YAWN_PROPAGATION_EXAMINE_WINDOW.</summary>
    private static readonly TimeSpan YawnExamineWindow = TimeSpan.FromSeconds(2);

    private readonly List<(TimeSpan Time, EntityUid Uid)> _pendingYawns = new();
    private readonly List<(TimeSpan Time, EntityUid Uid)> _pendingWakes = new();

    private void InitializeEffects()
    {
        SubscribeLocalEvent<ImperialEmotePanelComponent, ExaminedEvent>(OnExamined);
    }

    /// <summary>client.recent_examines: зевнувший сразу после осмотра заставляет осмотревшего зевнуть.</summary>
    private void OnExamined(Entity<ImperialEmotePanelComponent> ent, ref ExaminedEvent args)
    {
        if (args.Examiner == ent.Owner)
            return;

        EnsureComp<ImperialEmotePanelComponent>(args.Examiner).RecentExamines[ent.Owner] = _timing.CurTime;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        for (var i = _pendingYawns.Count - 1; i >= 0; i--)
        {
            var (time, uid) = _pendingYawns[i];
            if (now < time)
                continue;

            _pendingYawns.RemoveAt(i);
            if (!Exists(uid) || TryComp<ImperialEmotePanelComponent>(uid, out var panel) && now < panel.YawnImmuneUntil)
                continue;

            TryEmote(uid, "yawn");
        }

        for (var i = _pendingWakes.Count - 1; i >= 0; i--)
        {
            var (time, uid) = _pendingWakes[i];
            if (now < time)
                continue;

            _pendingWakes.RemoveAt(i);
            if (Exists(uid) && HasComp<SleepingComponent>(uid))
                _sleeping.TryWaking(uid);
        }
    }

    /// <summary>Тексты, зависящие от состояния: указание без рук и виляние хвостом.</summary>
    private string? ModifyMessage(EntityUid uid, ImperialEmotePrototype proto, string? param, string? message)
    {
        switch (proto.Effect)
        {
            case ImperialEmoteEffect.Wag when TryComp<WaggingComponent>(uid, out var wagging) && wagging.Wagging:
                return Loc.GetString("imperial-emote-msg-wag-stop", ("entity", uid));

            // point: без рук указывают ногой (и могут упасть) или многозначительным взглядом.
            case ImperialEmoteEffect.Point when param != null && IsKind(uid, ImperialEmoteMob.Carbon) && HandsBlocked(uid):
                if (_standing.IsDown(uid))
                    return Loc.GetString("imperial-emote-point-glance", ("target", param));

                var shoes = _inventory.TryGetSlotEntity(uid, "shoes", out _);
                if (_random.Prob(0.65f))
                    return Loc.GetString("imperial-emote-point-leg", ("target", param), ("shoes", shoes));

                _stun.TryUpdateParalyzeDuration(uid, TimeSpan.FromSeconds(2));
                return Loc.GetString("imperial-emote-point-leg-fail", ("target", param), ("shoes", shoes));
        }

        return message;
    }

    private void ApplyEffect(EntityUid uid, ImperialEmotePrototype proto)
    {
        switch (proto.Effect)
        {
            case ImperialEmoteEffect.None:
            case ImperialEmoteEffect.Point:
                return;

            case ImperialEmoteEffect.Collapse:
                // Unconscious(4 SECONDS).
                _status.TryAddStatusEffectDuration(uid, SleepingSystem.StatusEffectForcedSleeping, TimeSpan.FromSeconds(4));
                return;

            case ImperialEmoteEffect.Faint:
                // SetSleeping(20 SECONDS): обычный сон, от которого можно разбудить.
                if (_sleeping.TrySleeping(uid))
                    _pendingWakes.Add((_timing.CurTime + TimeSpan.FromSeconds(20), uid));
                return;

            case ImperialEmoteEffect.Surrender:
                _stun.TryUpdateParalyzeDuration(uid, TimeSpan.FromSeconds(20));
                return;

            case ImperialEmoteEffect.Yawn:
                PropagateYawn(uid);
                return;

            case ImperialEmoteEffect.Wag:
                RaiseLocalEvent(uid, new ToggleActionEvent());
                return;

            case ImperialEmoteEffect.AiDisplay:
                if (proto.AiDisplay != null)
                    SetAiDisplays(uid, proto.AiDisplay);
                return;
        }

        // Остальное — анимации и наложения, их рисуют клиенты рядом.
        RaiseNetworkEvent(new ImperialEmoteVisualMessage(GetNetEntity(uid), proto.Effect), Filter.Pvs(uid, entityManager: EntityManager));
    }

    /// <summary>check_cooldown у flip и spin: повтор раньше времени не проходит даром.</summary>
    private void OnCooldownFailed(EntityUid uid, ImperialEmotePrototype proto)
    {
        if (!CanRun(uid, proto, statusCheck: true))
            return;

        switch (proto.Effect)
        {
            case ImperialEmoteEffect.Flip:
            case ImperialEmoteEffect.Backflip:
                var fallChance = proto.Effect == ImperialEmoteEffect.Flip ? 0.6f : 0.2f;
                if (_random.Prob(fallChance))
                {
                    _stun.TryKnockdown(uid, TimeSpan.FromSeconds(1));
                    _popup.PopupEntity(Loc.GetString("imperial-emote-flip-fall-others", ("entity", uid)), uid, Filter.PvsExcept(uid), true);
                    _popup.PopupEntity(Loc.GetString("imperial-emote-flip-fall-self"), uid, uid);
                    if (_random.Prob(fallChance / 2))
                        _damageable.TryChangeDamage(uid, new DamageSpecifier(_proto.Index(Blunt), 1));
                }
                else
                {
                    _popup.PopupEntity(Loc.GetString("imperial-emote-flip-wobble-others", ("entity", uid)), uid, Filter.PvsExcept(uid), true);
                    _popup.PopupEntity(Loc.GetString("imperial-emote-flip-wobble-self"), uid, uid);
                }
                return;

            // BEYBLADE: от кручения на месте кружится голова.
            case ImperialEmoteEffect.Spin when IsKind(uid, ImperialEmoteMob.Carbon) && _random.Prob(0.2f):
                _popup.PopupEntity(Loc.GetString("imperial-emote-spin-dizzy"), uid, uid);
                _drunk.TryApplyDrunkenness(uid, TimeSpan.FromSeconds(20));
                return;
        }
    }

    /// <summary>yawn: соседи с шансом 20 - 4 × расстояние зевают следом.</summary>
    private void PropagateYawn(EntityUid uid)
    {
        var now = _timing.CurTime;
        var comp = EnsureComp<ImperialEmotePanelComponent>(uid);
        if (now >= comp.YawnImmuneUntil)
            comp.YawnImmuneUntil = now + TimeSpan.FromSeconds(15);

        // obscured_slots & HIDEFACE: под маской зевок не видно.
        if (_inventory.TryGetSlotEntity(uid, "mask", out var mask) && TryComp<IdentityBlockerComponent>(mask, out var blocker) && blocker.Enabled)
            return;

        var range = HasComp<ActorComponent>(uid) ? 5 : 2;
        var origin = _transform.GetMapCoordinates(uid);
        foreach (var other in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(uid).Coordinates, range))
        {
            if (other.Owner == uid || _mobState.IsIncapacitated(other) || HasComp<SleepingComponent>(other))
                continue;

            var otherPanel = CompOrNull<ImperialEmotePanelComponent>(other);
            if (otherPanel != null && now < otherPanel.YawnImmuneUntil)
                continue;

            if (!_examine.InRangeUnOccluded(uid, other, range))
                continue;

            var distance = (int) MathF.Round((_transform.GetMapCoordinates(other).Position - origin.Position).Length());
            var examined = otherPanel != null && otherPanel.RecentExamines.TryGetValue(uid, out var time) && now - time < YawnExamineWindow;
            if (!examined && !_random.Prob(Math.Clamp(YawnChanceBase - YawnChanceDecay * distance, 0, 100) / 100f))
                continue;

            _pendingYawns.Add((now + TimeSpan.FromSeconds(_random.NextFloat(0.2f, 0.7f) * distance), other));
        }
    }

    /// <summary>apply_emote_display: все экраны станции ИИ показывают картинку эмоции.</summary>
    private void SetAiDisplays(EntityUid ai, string state)
    {
        if (!Factory.TryGetRegistration(AiDisplayComponent, out var reg) || reg.Type.GetField("State") is not { } field)
            return;

        var station = _stationAi.TryGetCore(ai, out var core) ? _station.GetOwningStation(core.Owner) : null;
        var query = EntityQueryEnumerator<ScreenComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (station != null && _station.GetOwningStation(uid, xform) != station)
                continue;

            if (!EntityManager.TryGetComponent(uid, reg.Type, out var display))
            {
                display = Factory.GetComponent(reg);
                AddComp(uid, display);
            }

            field.SetValue(display, state);
            Dirty(uid, display);
        }
    }
}

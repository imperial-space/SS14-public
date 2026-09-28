using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticCaretakerRefugeSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem      _actions    = default!;
    [Dependency] private readonly SharedAudioSystem        _audio      = default!;
    [Dependency] private readonly EntityLookupSystem       _lookup     = default!;
    [Dependency] private readonly SharedPhysicsSystem      _physics    = default!;
    [Dependency] private readonly SharedPopupSystem        _popup      = default!;
    [Dependency] private readonly SharedStealthSystem      _stealth    = default!;
    [Dependency] private readonly IGameTiming              _timing     = default!;

    private const string ExitActionProto = "ActionHereticCaretakerRefugeExit";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HereticCaretakerRefugeActiveComponent, AttackAttemptEvent>(OnAttack);
        SubscribeLocalEvent<HereticCaretakerRefugeActiveComponent, UseAttemptEvent>(OnUseAttempt);
        SubscribeLocalEvent<HereticCaretakerRefugeActiveComponent, PickupAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<HereticCaretakerRefugeActiveComponent, InteractionAttemptEvent>(OnInteractAttempt);
        SubscribeLocalEvent<HereticCaretakerRefugeActiveComponent, BeforeDamageChangedEvent>(OnBeforeDamage);

        SubscribeLocalEvent<HereticComponent, HereticCaretakerRefugeActionEvent>(OnRefuge);
        SubscribeLocalEvent<HereticComponent, HereticCaretakerRefugeExitActionEvent>(OnRefugeExit);
    }

    // ─── Блокировка действий в убежище ───────────────────────────────────────

    private void OnAttack(Entity<HereticCaretakerRefugeActiveComponent> ent, ref AttackAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnUseAttempt(Entity<HereticCaretakerRefugeActiveComponent> ent, ref UseAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnPickupAttempt(Entity<HereticCaretakerRefugeActiveComponent> ent, ref PickupAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnInteractAttempt(Entity<HereticCaretakerRefugeActiveComponent> ent, ref InteractionAttemptEvent args)
    {
        args.Cancelled = true;
    }

    // ─── Урон: Holy ломает убежище, остальное — блокируется ──────────────────

    private void OnBeforeDamage(Entity<HereticCaretakerRefugeActiveComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Damage.GetTotal() <= FixedPoint2.Zero)
            return;

        if (args.Damage.DamageDict.TryGetValue("Holy", out var holy) && holy > FixedPoint2.Zero)
        {
            // Антимагия — снять убежище и пропустить урон
            DeactivateRefuge(ent.Owner, ent.Comp, popupKey: "heretic-caretaker-refuge-broken-holy");
        }
        else
        {
            // Любой другой урон — заблокировать
            args.Cancelled = true;
        }
    }

    // ─── Активация / деактивация ──────────────────────────────────────────────

    private void OnRefuge(EntityUid uid, HereticComponent comp, HereticCaretakerRefugeActionEvent args)
    {
        if (args.Handled) return;

        // Если убежище уже активно — выход через основной ивент не работает (заклинания заблокированы).
        // Используется отдельное действие с checkCanInteract: false.
        if (HasComp<HereticCaretakerRefugeActiveComponent>(uid))
            return;

        // Нельзя использовать рядом с живыми разумными существами
        if (HasNearbySentient(uid))
        {
            _popup.PopupEntity(Loc.GetString("heretic-caretaker-refuge-sentients-nearby"), uid, uid, PopupType.MediumCaution);
            return;
        }

        args.Handled = true;

        // Убрать кулдаун при активации
        _actions.ClearCooldown(new Entity<ActionComponent?>(args.Action.Owner, args.Action.Comp));

        ActivateRefuge(uid, args.Action.Owner);
    }

    private void OnRefugeExit(EntityUid uid, HereticComponent comp, HereticCaretakerRefugeExitActionEvent args)
    {
        if (args.Handled) return;
        if (!TryComp<HereticCaretakerRefugeActiveComponent>(uid, out var refugeComp)) return;
        args.Handled = true;
        DeactivateRefuge(uid, refugeComp);
    }

    // ─── Внутренняя логика ────────────────────────────────────────────────────

    private void ActivateRefuge(EntityUid uid, EntityUid mainActionEntity)
    {
        if (!TryComp<PhysicsComponent>(uid, out var physics) ||
            !TryComp<FixturesComponent>(uid, out var fixtures))
            return;

        var refuge = EnsureComp<HereticCaretakerRefugeActiveComponent>(uid);
        refuge.FixtureStates.Clear();
        refuge.MainActionEntity = mainActionEntity;

        // Бестелесность через физику
        foreach (var (id, fixture) in fixtures.Fixtures)
        {
            refuge.FixtureStates.Add((id, fixture.Hard, fixture.CollisionLayer, fixture.CollisionMask));
            _physics.SetHard(uid, fixture, false, fixtures);
            _physics.SetCollisionLayer(uid, id, fixture, (int) CollisionGroup.None, fixtures, physics);
            _physics.SetCollisionMask(uid, id, fixture, (int) CollisionGroup.None, fixtures, physics);
        }

        // Прозрачность
        refuge.AddedStealth = !HasComp<StealthComponent>(uid);
        var stealthComp = EnsureComp<StealthComponent>(uid);
        _stealth.SetEnabled(uid, true, stealthComp);
        _stealth.SetVisibility(uid, 0.3f, stealthComp);

        // Иммунитет к замедлению
        EnsureComp<IgnoreSlowOnDamageComponent>(uid);

        // Выдать действие выхода (с checkCanInteract: false в YAML)
        EntityUid? exitAction = null;
        _actions.AddAction(uid, ref exitAction, ExitActionProto);
        refuge.ExitActionEntity = exitAction;

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-caretaker-refuge-activate"), uid, uid, PopupType.Medium);
    }

    private void DeactivateRefuge(EntityUid uid, HereticCaretakerRefugeActiveComponent refuge, string popupKey = "heretic-caretaker-refuge-deactivate")
    {
        // Восстановить физику
        if (TryComp<PhysicsComponent>(uid, out var physics) && TryComp<FixturesComponent>(uid, out var fixtures))
        {
            foreach (var (id, hard, layer, mask) in refuge.FixtureStates)
            {
                if (!fixtures.Fixtures.TryGetValue(id, out var fixture)) continue;
                _physics.SetHard(uid, fixture, hard, fixtures);
                _physics.SetCollisionLayer(uid, id, fixture, layer, fixtures, physics);
                _physics.SetCollisionMask(uid, id, fixture, mask, fixtures, physics);
            }
        }

        // Убрать прозрачность
        if (refuge.AddedStealth)
            RemComp<StealthComponent>(uid);
        else if (TryComp<StealthComponent>(uid, out var sc))
            _stealth.SetEnabled(uid, false, sc);

        // Убрать иммунитет к замедлению
        RemCompDeferred<IgnoreSlowOnDamageComponent>(uid);

        // Убрать действие выхода
        if (refuge.ExitActionEntity.HasValue)
            _actions.RemoveAction(new Entity<ActionComponent?>(refuge.ExitActionEntity.Value, null));

        // Установить кулдаун на основное действие
        if (refuge.MainActionEntity.HasValue)
        {
            var now = _timing.CurTime;
            _actions.SetCooldown(
                new Entity<ActionComponent?>(refuge.MainActionEntity.Value, CompOrNull<ActionComponent>(refuge.MainActionEntity.Value)),
                now, now + TimeSpan.FromSeconds(60));
        }

        RemComp<HereticCaretakerRefugeActiveComponent>(uid);

        _popup.PopupEntity(Loc.GetString(popupKey), uid, uid, PopupType.Medium);
    }

    // ─── Вспомогательные методы ───────────────────────────────────────────────

    private bool HasNearbySentient(EntityUid self)
    {
        var coords = Transform(self).Coordinates;
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 5f))
        {
            if (ent.Owner == self)
                continue;
            if (TryComp<MobStateComponent>(ent.Owner, out var mob) && mob.CurrentState == MobState.Dead)
                continue;
            return true;
        }
        return false;
    }
}

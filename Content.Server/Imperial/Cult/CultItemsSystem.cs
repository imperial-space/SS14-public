using System.Linq;
using Content.Server.Imperial.Cult.Components;
using Content.Server.Popups;
using Content.Server.RoundEnd;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.Cult;
using Content.Shared.Imperial.Cult.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Overlays;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Robust.Shared.GameObjects;
using Robust.Shared.Random;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Imperial.Cult;

/// <summary>
/// Обрабатывает логику всех специальных предметов культа:
/// Сдвигатель вуали, Проклятая сфера, Жуткое точило, Флакон, Пустая оболочка, Повязка зилота.
/// </summary>
public sealed class CultItemsSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly ThrownItemSystem _thrown = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    // Глобальный счётчик: сколько раз за раунд можно использовать Проклятую сферу.
    private int _cursedOrbUsesLeft = 2;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<CultVeilShifterComponent, UseInHandEvent>(OnVeilShifterUse);
        SubscribeLocalEvent<CultCursedOrbComponent, UseInHandEvent>(OnCursedOrbUse);
        SubscribeLocalEvent<CultWhetstoneComponent, AfterInteractEvent>(OnWhetstoneInteract);
        SubscribeLocalEvent<CultUnholyFlaskComponent, UseInHandEvent>(OnUnholyFlaskUse);
        SubscribeLocalEvent<CultEmptyShellComponent, UseInHandEvent>(OnEmptyShellUse);
        SubscribeLocalEvent<CultConstructShellComponent, InteractUsingEvent>(OnConstructShellInteractUsing);
        SubscribeLocalEvent<CultZealotBlindfoldComponent, GotEquippedEvent>(OnBlindfoldEquipped);
        SubscribeLocalEvent<CultZealotBlindfoldComponent, GotUnequippedEvent>(OnBlindfoldUnequipped);
        SubscribeLocalEvent<CultBloodOrbComponent, UseInHandEvent>(OnBloodOrbUseInHand);
        SubscribeLocalEvent<CultBloodOrbComponent, AfterInteractEvent>(OnBloodOrbAfterInteract);
        SubscribeLocalEvent<CultBloodSpearComponent, ThrowDoHitEvent>(OnBloodSpearThrowHit);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent _)
    {
        _cursedOrbUsesLeft = 2;
    }

    // ─── Сдвигатель вуали ─────────────────────────────────────────────────

    private void OnVeilShifterUse(EntityUid uid, CultVeilShifterComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (comp.Charges <= 0)
        {
            _popup.PopupEntity(Loc.GetString("cult-veil-shifter-exhausted"), uid, args.User);
            args.Handled = true;
            return;
        }

        var tiles = _random.Next(comp.MinTiles, comp.MaxTiles + 1);
        var userXform = Transform(args.User);
        var direction = _xform.GetWorldRotation(args.User).GetCardinalDir().ToVec();
        var targetPos = userXform.WorldPosition + direction * tiles;
        _xform.SetWorldPosition(args.User, targetPos);

        comp.Charges--;
        _appearance.SetData(uid, CultVeilShifterVisuals.Depleted, comp.Charges <= 0);

        _popup.PopupEntity(
            Loc.GetString("cult-veil-shifter-used", ("charges", comp.Charges)),
            args.User, args.User, PopupType.Small);

        if (comp.Charges <= 0)
            _popup.PopupEntity(Loc.GetString("cult-veil-shifter-exhausted"), args.User, args.User, PopupType.Small);

        args.Handled = true;
    }

    // ─── Проклятая сфера ──────────────────────────────────────────────────

    private void OnCursedOrbUse(EntityUid uid, CultCursedOrbComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (_cursedOrbUsesLeft <= 0)
        {
            _popup.PopupEntity(Loc.GetString("cult-cursed-orb-exhausted"), uid, args.User);
            QueueDel(uid);
            args.Handled = true;
            return;
        }

        if (!_roundEnd.IsRoundEndRequested())
        {
            _popup.PopupEntity(Loc.GetString("cult-cursed-orb-no-shuttle"), uid, args.User);
            args.Handled = true;
            return;
        }

        var remaining = _roundEnd.ShuttleTimeLeft ?? TimeSpan.Zero;
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        var newTime = remaining + comp.DelayAmount;

        // Отменяем текущий таймер и перезапускаем с добавленным временем.
        _roundEnd.CancelRoundEndCountdown(forceRecall: true);
        _roundEnd.RequestRoundEnd(newTime, args.User, checkCooldown: false);

        _cursedOrbUsesLeft--;
        QueueDel(uid);

        _popup.PopupEntity(
            Loc.GetString("cult-cursed-orb-used", ("uses", _cursedOrbUsesLeft)),
            args.User, args.User, PopupType.Medium);

        args.Handled = true;
    }

    // ─── Жуткое точило ────────────────────────────────────────────────────

    private void OnWhetstoneInteract(EntityUid uid, CultWhetstoneComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        var target = args.Target.Value;

        // Не точим живых существ
        if (target == args.User)
            return;

        // Точило уже израсходовано
        if (comp.Used)
        {
            _popup.PopupEntity(Loc.GetString("cult-whetstone-already-used"), uid, args.User);
            args.Handled = true;
            return;
        }

        if (!TryComp<MeleeWeaponComponent>(target, out var melee))
            return;

        if (HasComp<CultSharpenedComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("cult-whetstone-already-sharpened"), uid, args.User);
            args.Handled = true;
            return;
        }

        // Добавляем бонус к первому типу урона (slash или любой другой)
        if (melee.Damage.DamageDict.Count == 0)
            return;

        var primaryKey = melee.Damage.DamageDict.Keys
            .OrderByDescending(k => melee.Damage.DamageDict[k])
            .First();

        melee.Damage.DamageDict[primaryKey] = melee.Damage.DamageDict[primaryKey] + (int)comp.DamageBonus;

        Dirty(target, melee);
        EnsureComp<CultSharpenedComponent>(target);
        comp.Used = true;
        _appearance.SetData(uid, CultWhetstoneVisuals.Used, true);

        _popup.PopupEntity(Loc.GetString("cult-whetstone-sharpened"), uid, args.User, PopupType.Medium);
        args.Handled = true;
    }

    // ─── Флакон проклятой воды ────────────────────────────────────────────

    private void OnUnholyFlaskUse(EntityUid uid, CultUnholyFlaskComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (comp.UsesLeft <= 0)
        {
            _popup.PopupEntity(Loc.GetString("cult-unholy-flask-exhausted"), uid, args.User);
            args.Handled = true;
            return;
        }

        if (!HasComp<CultistComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("cult-unholy-flask-not-cultist"), uid, args.User);
            args.Handled = true;
            return;
        }

        var healing = new DamageSpecifier();
        healing.DamageDict.Add("Brute", -(double)comp.HealAmount);
        healing.DamageDict.Add("Burn", -(double)comp.HealAmount);
        _damage.TryChangeDamage(args.User, healing, true);

        // Снимаем оглушение
        _stun.TryUnstun(args.User);

        comp.UsesLeft--;

        _popup.PopupEntity(Loc.GetString("cult-unholy-flask-used"), args.User, args.User, PopupType.Medium);

        if (comp.UsesLeft <= 0)
            QueueDel(uid);

        args.Handled = true;
    }

    // ─── Пустая оболочка ──────────────────────────────────────────────────

    private void OnEmptyShellUse(EntityUid uid, CultEmptyShellComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        _popup.PopupEntity(Loc.GetString("cult-empty-shell-use"), uid, args.User);
        args.Handled = true;
    }

    private void OnConstructShellInteractUsing(EntityUid uid, CultConstructShellComponent comp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<CultSoulStoneMarkerComponent>(args.Used) && MetaData(args.Used).EntityPrototype?.ID != "CultSoulStone")
            return;

        if (!HasComp<CultistComponent>(args.User) && !HasComp<CultConstructComponent>(args.User))
            return;

        args.Handled = true;

        var coords = Transform(uid).Coordinates;
        Spawn(comp.FormationEffectProto, coords);

        QueueDel(args.Used);
        QueueDel(uid);

        Timer.Spawn(1000, () => Spawn(comp.ConstructProto, coords));
    }

    // ─── Повязка зилота ───────────────────────────────────────────────────

    private void OnBlindfoldEquipped(EntityUid uid, CultZealotBlindfoldComponent comp, GotEquippedEvent args)
    {
        comp.AddedHealthBars = !HasComp<ShowHealthBarsComponent>(args.EquipTarget);
        if (comp.AddedHealthBars)
            EnsureComp<ShowHealthBarsComponent>(args.EquipTarget);

        if (TryComp<EyeComponent>(args.EquipTarget, out var eye))
        {
            comp.HadEyeState = true;
            comp.PreviousDrawLight = eye.DrawLight;
            _eye.SetDrawLight((args.EquipTarget, eye), false);
        }
        else
        {
            comp.HadEyeState = false;
        }
    }

    private void OnBlindfoldUnequipped(EntityUid uid, CultZealotBlindfoldComponent comp, GotUnequippedEvent args)
    {
        if (comp.AddedHealthBars)
            RemComp<ShowHealthBarsComponent>(args.EquipTarget);

        if (comp.HadEyeState && TryComp<EyeComponent>(args.EquipTarget, out var eye))
            _eye.SetDrawLight((args.EquipTarget, eye), comp.PreviousDrawLight);

        comp.AddedHealthBars = false;
        comp.HadEyeState = false;
    }

    // ─── Кровавая сфера ─────────────────────────────────────────────────────

    private void OnBloodOrbUseInHand(EntityUid uid, CultBloodOrbComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<CultistComponent>(args.User, out var cultist))
        {
            _popup.PopupEntity(Loc.GetString("cult-blood-orb-not-cultist"), uid, args.User);
            args.Handled = true;
            return;
        }

        cultist.BloodRitesCharges += comp.Charges;
        _popup.PopupEntity(Loc.GetString("cult-blood-orb-absorbed", ("charges", comp.Charges)), args.User, args.User, PopupType.Small);
        QueueDel(uid);
        args.Handled = true;
    }

    private void OnBloodOrbAfterInteract(EntityUid uid, CultBloodOrbComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!TryComp<CultistComponent>(args.User, out _))
            return;

        if (!TryComp<CultistComponent>(target, out var targetCultist))
            return;

        targetCultist.BloodRitesCharges += comp.Charges;
        _popup.PopupEntity(Loc.GetString("cult-blood-orb-transferred", ("charges", comp.Charges)), args.User, args.User, PopupType.Small);
        _popup.PopupEntity(Loc.GetString("cult-blood-orb-received", ("charges", comp.Charges)), target, target, PopupType.Small);
        QueueDel(uid);
        args.Handled = true;
    }

    private void OnBloodSpearThrowHit(EntityUid uid, CultBloodSpearComponent comp, ref ThrowDoHitEvent args)
    {
        _thrown.StopThrow(uid, args.Component);

        if (TryComp<CultistComponent>(args.Target, out _))
        {
            _hands.TryPickupAnyHand(args.Target, uid, animate: false);
            return;
        }

        if (!HasComp<DamageableComponent>(args.Target))
            return;

        var damage = new DamageSpecifier();
        damage.DamageDict.Add("Blunt", 40f);
        _damage.TryChangeDamage(args.Target, damage, false, origin: args.Component.Thrower);
    }
}

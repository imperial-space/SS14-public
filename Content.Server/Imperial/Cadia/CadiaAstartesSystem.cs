using System.Numerics;
using Content.Server.Body.Systems;
using Content.Server.DoAfter;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Imperial.ZLevels;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Космодесант и особые роли: органы Астартес, силовой доспех только поверх чёрного панциря, геносемя, прыжковый
/// ранец, метки богов Хаоса. Заодно действия роли: молитва священника, Акт веры, Экстерминатус.
/// </summary>
public sealed class CadiaAstartesSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly BloodstreamSystem _blood = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly CadiaDefenseRuleSystem _rule = default!;
    [Dependency] private readonly CadiaPsykerSystem _psyker = default!;
    [Dependency] private readonly CadiaMoraleSystem _morale = default!;
    [Dependency] private readonly Content.Shared.Throwing.ThrowingSystem _throwing = default!;

    private static readonly SoundSpecifier JumpSound = new SoundPathSpecifier("/Audio/Effects/Vehicle/vehiclestartup.ogg");
    private static readonly SoundSpecifier LandSound = new SoundPathSpecifier("/Audio/Effects/explosion_small1.ogg");
    private static readonly SoundSpecifier PrayerSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/charge.ogg");
    private static readonly SoundSpecifier FaithSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/knock.ogg");
    private static readonly SoundSpecifier WardSound = new SoundPathSpecifier("/Audio/Imperial/MiningWeapons/Smasher/activate_shield.ogg");
    private static readonly SoundSpecifier ReductorSound = new SoundPathSpecifier("/Audio/Items/ratchet.ogg");

    private static readonly ProtoId<DamageTypePrototype> BluntDamage = "Blunt";
    private static readonly TimeSpan WardCooldown = TimeSpan.FromSeconds(30);
    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();
    private TimeSpan _nextTick;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CadiaAstartesComponent, DamageModifyEvent>(OnAstartesDamage);
        SubscribeLocalEvent<CadiaPowerArmorComponent, BeingEquippedAttemptEvent>(OnArmorEquip);
        SubscribeLocalEvent<CadiaReductorComponent, AfterInteractEvent>(OnReductor);
        SubscribeLocalEvent<CadiaAstartesComponent, CadiaReductorDoAfterEvent>(OnReductorDone);
        SubscribeLocalEvent<CadiaGeneSeedComponent, UseInHandEvent>(OnGeneSeed);

        SubscribeLocalEvent<CadiaJumpActionEvent>(OnJump);

        SubscribeLocalEvent<CadiaRoleActionsComponent, MapInitEvent>(OnRoleActionsInit);
        SubscribeLocalEvent<CadiaPrayerActionEvent>(OnPrayer);
        SubscribeLocalEvent<CadiaActOfFaithActionEvent>(OnActOfFaith);
        SubscribeLocalEvent<CadiaFaithShieldComponent, DamageModifyEvent>(OnFaithDamage);
        SubscribeLocalEvent<CadiaJumpPackComponent, GotEquippedEvent>(OnPackEquipped);
        SubscribeLocalEvent<CadiaJumpPackComponent, GotUnequippedEvent>(OnPackUnequipped);
        SubscribeLocalEvent<CadiaExterminatusActionEvent>(OnExterminatus);
        SubscribeLocalEvent<CadiaFactionMemberComponent, CadiaExterminatusDoAfterEvent>(OnExterminatusDone);

        SubscribeLocalEvent<CadiaMarkCandidateComponent, MapInitEvent>(OnCandidateInit);
        SubscribeLocalEvent<CadiaMarkCandidateComponent, CadiaChooseMarkActionEvent>(OnChooseMark);
        SubscribeLocalEvent<CadiaChaosMarkComponent, DamageModifyEvent>(OnMarkDamage);
        SubscribeLocalEvent<CadiaChaosMarkComponent, GetMeleeDamageEvent>(OnMarkMelee);
        SubscribeLocalEvent<CadiaChaosMarkComponent, RefreshMovementSpeedModifiersEvent>(OnMarkSpeed);
    }

    public void GrantRoleActions(Entity<CadiaRoleActionsComponent> ent)
    {
        foreach (var action in ent.Comp.Actions)
            _actions.AddAction(ent, action);
    }

    public void GrantMarkChoices(Entity<CadiaMarkCandidateComponent> ent)
    {
        if (ent.Comp.Actions.Count > 0)
            return;
        foreach (var proto in new[] { "ActionCadiaMarkKhorne", "ActionCadiaMarkNurgle", "ActionCadiaMarkTzeentch", "ActionCadiaMarkSlaanesh" })
        {
            if (_actions.AddAction(ent, proto) is { } action)
                ent.Comp.Actions.Add(action);
        }
    }

    private void OnRoleActionsInit(Entity<CadiaRoleActionsComponent> ent, ref MapInitEvent args)
    {
        GrantRoleActions(ent);
    }

    private void OnCandidateInit(Entity<CadiaMarkCandidateComponent> ent, ref MapInitEvent args)
    {
        GrantMarkChoices(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        // Приземление: проверяем каждый кадр, чтобы удар совпадал с анимацией.
        var packs = EntityQueryEnumerator<CadiaJumpPackComponent>();
        while (packs.MoveNext(out var uid, out var pack))
        {
            if (pack.LandAt is { } land && now >= land)
                Land(uid, pack);
        }

        if (now < _nextTick)
            return;
        _nextTick = now + TimeSpan.FromSeconds(1);

        var query = EntityQueryEnumerator<CadiaAstartesComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var astartes, out var state))
        {
            if (state.CurrentState == MobState.Dead)
                continue;
            _blood.TryModifyBleedAmount(uid, -astartes.BleedStop);
            if (state.CurrentState == MobState.Critical)
                _damage.HealEvenly(uid, -astartes.AnabiosisHeal);
        }

        var shields = EntityQueryEnumerator<CadiaFaithShieldComponent>();
        while (shields.MoveNext(out var uid, out var shield))
        {
            if (now >= shield.Until)
                RemCompDeferred<CadiaFaithShieldComponent>(uid);
        }
    }

    // ═══════════════════════ Органы ═══════════════════════

    /// <summary>Преомнор — яды почти не действуют, оссмодула — тупой урон слабее.</summary>
    private void OnAstartesDamage(Entity<CadiaAstartesComponent> ent, ref DamageModifyEvent args)
    {
        // Урон часто общий с прототипом оружия: правим только копию.
        args.Damage = new DamageSpecifier(args.Damage);
        var dict = args.Damage.DamageDict;
        if (dict.TryGetValue("Poison", out var poison))
            dict["Poison"] = poison * 0.1f;
        if (dict.TryGetValue("Blunt", out var blunt))
            dict["Blunt"] = blunt * 0.8f;
    }

    private void OnArmorEquip(Entity<CadiaPowerArmorComponent> ent, ref BeingEquippedAttemptEvent args)
    {
        if (!ent.Comp.RequiresCarapace || HasComp<CadiaAstartesComponent>(args.EquipTarget))
            return;
        args.Cancel();
        args.Reason = "cadia-armor-no-carapace";
    }

    // ═══════════════════════ Геносемя ═══════════════════════

    private void OnReductor(Entity<CadiaReductorComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;
        if (!TryComp<CadiaAstartesComponent>(target, out var astartes))
            return;
        args.Handled = true;
        if (!_mobState.IsDead(target) || !astartes.HasProgenoid)
        {
            _popup.PopupEntity(Loc.GetString(astartes.HasProgenoid ? "cadia-reductor-alive" : "cadia-reductor-empty"), target, args.User);
            return;
        }
        var doAfter = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(6), new CadiaReductorDoAfterEvent(), target, target, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };
        if (_doAfter.TryStartDoAfter(doAfter))
            _audio.PlayPvs(ReductorSound, target);
    }

    private void OnReductorDone(Entity<CadiaAstartesComponent> ent, ref CadiaReductorDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !ent.Comp.HasProgenoid)
            return;
        args.Handled = true;
        ent.Comp.HasProgenoid = false;
        var seed = Spawn("CadiaGeneSeed", Transform(ent).Coordinates);
        _hands.TryPickupAnyHand(args.User, seed);
        _popup.PopupEntity(Loc.GetString("cadia-reductor-done"), ent, args.User, PopupType.Medium);
    }

    /// <summary>Своё геносемя возвращает брата в строй в следующей волне; Хаос приносит его в жертву варпу.</summary>
    private void OnGeneSeed(Entity<CadiaGeneSeedComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !TryComp<CadiaFactionMemberComponent>(args.User, out var member))
            return;
        args.Handled = true;
        if (member.Faction == CadiaFaction.Chaos)
        {
            _rule.AddWarp(ent.Comp.WarpValue);
            _popup.PopupEntity(Loc.GetString("cadia-seed-sacrificed", ("warp", ent.Comp.WarpValue)), args.User, args.User, PopupType.Medium);
        }
        else
        {
            _rule.AddBonusSlot("CadiaUltramarineTactical");
            _popup.PopupEntity(Loc.GetString("cadia-seed-consecrated"), args.User, args.User, PopupType.Medium);
        }
        QueueDel(ent);
    }

    // ═══════════════════════ Прыжковый ранец ═══════════════════════

    private void OnJump(CadiaJumpActionEvent args)
    {
        if (args.Handled || !TryComp<CadiaJumpPackComponent>(args.Action.Comp.Container, out var pack))
            return;
        args.Handled = true;
        var user = args.Performer;
        var from = _xform.GetMapCoordinates(user);
        var to = _xform.ToMapCoordinates(args.Target);
        if (from.MapId != to.MapId)
            return;
        var delta = to.Position - from.Position;
        if (delta.Length() > pack.Range)
            delta = delta.Normalized() * pack.Range;

        // Прыжок — бросок самого себя: стены останавливают, удар приходит в момент приземления.
        const float JumpSpeed = 14f;
        _throwing.TryThrow(user, delta, JumpSpeed, user, recoil: false, animated: false, doSpin: false);
        pack.LandAt = _timing.CurTime + TimeSpan.FromSeconds(delta.Length() / JumpSpeed);
        pack.Jumper = user;
        Spawn("CadiaPsyRingBlue", from);
        _audio.PlayPvs(JumpSound, user);
    }

    /// <summary>
    /// Ранец надет — десантник летает, как Хелдрейк: над проёмами не падает, вверх и вниз уходит действиями ранца.
    /// Полёт снимается вместе с ранцем, если до этого десантник не летал сам.
    /// </summary>
    private void OnPackEquipped(EntityUid uid, CadiaJumpPackComponent pack, GotEquippedEvent args)
    {
        if (HasComp<ZFlyerComponent>(args.EquipTarget))
            return;
        AddComp<ZFlyerComponent>(args.EquipTarget);
        pack.Flyer = args.EquipTarget;
    }

    private void OnPackUnequipped(EntityUid uid, CadiaJumpPackComponent pack, GotUnequippedEvent args)
    {
        if (pack.Flyer != args.EquipTarget)
            return;
        RemComp<ZFlyerComponent>(args.EquipTarget);
        pack.Flyer = null;
    }

    private void Land(EntityUid packUid, CadiaJumpPackComponent pack)
    {
        pack.LandAt = null;
        if (pack.Jumper is not { } user || TerminatingOrDeleted(user))
            return;
        var coords = _xform.GetMapCoordinates(user);
        Spawn("CadiaJumpImpactEffect", coords);
        _audio.PlayPvs(LandSound, user);
        var faction = CompOrNull<CadiaFactionMemberComponent>(user)?.Faction;
        _mobs.Clear();
        _lookup.GetEntitiesInRange(coords, pack.ImpactRadius, _mobs);
        var blunt = _proto.Index(BluntDamage);
        foreach (var mob in _mobs)
        {
            if (mob.Owner == user)
                continue;
            if (faction != null && TryComp<CadiaFactionMemberComponent>(mob, out var other) && other.Faction == faction)
                continue;
            _damage.TryChangeDamage(mob.Owner, new DamageSpecifier(blunt, pack.ImpactDamage), origin: user);
            _stun.TryUpdateParalyzeDuration(mob, TimeSpan.FromSeconds(1.5));
        }
    }

    // ═══════════════════════ Вера ═══════════════════════

    private void OnPrayer(CadiaPrayerActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        var user = args.Performer;
        var faction = CompOrNull<CadiaFactionMemberComponent>(user)?.Faction;
        var coords = _xform.GetMapCoordinates(user);
        _mobs.Clear();
        _lookup.GetEntitiesInRange(coords, 6f, _mobs);
        foreach (var mob in _mobs)
        {
            if (!TryComp<CadiaFactionMemberComponent>(mob, out var other) || other.Faction != faction)
                continue;
            _damage.HealEvenly(mob.Owner, -8);
            _morale.Change(mob, 25f);
            if (TryComp<CadiaPsykerComponent>(mob, out var psyker))
                _psyker.Soothe((mob, psyker), 30f);
        }
        Spawn("CadiaFaithRingEffect", coords);
        _audio.PlayPvs(PrayerSound, user);
    }

    private void OnActOfFaith(CadiaActOfFaithActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        var user = args.Performer;
        EnsureComp<CadiaFaithShieldComponent>(user).Until = _timing.CurTime + TimeSpan.FromSeconds(8);
        _damage.HealEvenly(user, -25);
        Spawn("CadiaFaithRingEffect", _xform.GetMapCoordinates(user));
        _audio.PlayPvs(FaithSound, user);
    }

    private void OnFaithDamage(Entity<CadiaFaithShieldComponent> ent, ref DamageModifyEvent args)
    {
        args.Damage *= 0.4f;
    }

    private void OnExterminatus(CadiaExterminatusActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        var user = args.Performer;
        _popup.PopupEntity(Loc.GetString("cadia-exterminatus-begin"), user, PopupType.LargeCaution);
        var doAfter = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(30), new CadiaExterminatusDoAfterEvent(), user)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnExterminatusDone(Entity<CadiaFactionMemberComponent> ent, ref CadiaExterminatusDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;
        args.Handled = true;
        _rule.Exterminatus();
    }

    // ═══════════════════════ Метки богов ═══════════════════════

    private void OnChooseMark(Entity<CadiaMarkCandidateComponent> ent, ref CadiaChooseMarkActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        var mark = EnsureComp<CadiaChaosMarkComponent>(ent);
        mark.God = args.God;
        Dirty(ent, mark);
        foreach (var action in ent.Comp.Actions)
            _actions.RemoveAction(ent.Owner, action);
        RemCompDeferred<CadiaMarkCandidateComponent>(ent);
        _speed.RefreshMovementSpeedModifiers(ent);
        Spawn("CadiaPsyRingRed", _xform.GetMapCoordinates(ent));
        _audio.PlayPvs(FaithSound, ent);
        _popup.PopupEntity(Loc.GetString($"cadia-mark-{args.God.ToString().ToLowerInvariant()}"), ent, ent, PopupType.Large);
    }

    /// <summary>Нургл — живучесть, Тзинч — щит варпа на одно попадание раз в 30 секунд.</summary>
    private void OnMarkDamage(Entity<CadiaChaosMarkComponent> ent, ref DamageModifyEvent args)
    {
        switch (ent.Comp.God)
        {
            case CadiaChaosGod.Nurgle:
                args.Damage *= 0.8f;
                break;
            case CadiaChaosGod.Tzeentch:
                var now = _timing.CurTime;
                if (now < ent.Comp.NextWard || args.Damage.GetTotal() <= 0)
                    break;
                ent.Comp.NextWard = now + WardCooldown;
                args.Damage *= 0f;
                _audio.PlayPvs(WardSound, ent);
                _popup.PopupEntity(Loc.GetString("cadia-mark-ward"), ent, PopupType.Medium);
                break;
        }
    }

    private void OnMarkMelee(Entity<CadiaChaosMarkComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (ent.Comp.God == CadiaChaosGod.Khorne)
            args.Damage *= 1.25f;
    }

    private void OnMarkSpeed(Entity<CadiaChaosMarkComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.God == CadiaChaosGod.Slaanesh)
            args.ModifySpeed(1.2f, 1.2f);
    }
}

using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.Popups;
using Content.Server.Weapons.Melee;
using Content.Shared.Actions;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.MartialArts;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Popups;
using Content.Shared.Pulling.Events;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.MartialArts;

/// <summary>Итог приёма: MARTIAL_ATTACK_INVALID / FAIL / SUCCESS.</summary>
public enum MartialResult : byte
{
    /// <summary>Приём не подошёл — обычная атака.</summary>
    Invalid,

    /// <summary>Приём сорвался, обычной атаки тоже нет.</summary>
    Fail,

    /// <summary>Приём удался, обычной атаки нет.</summary>
    Success,

    /// <summary>Только для захвата: приём удался, и захват при этом состоялся.</summary>
    SuccessKeepGrab,
}

/// <summary>Кто нанёс удар, чтобы правильно посчитать блок (attack_type в check_block).</summary>
public enum MartialAttackType : byte
{
    Unarmed,
    Melee,
    Projectile,
    Thrown,
}

/// <summary>
/// Боевые искусства 1 в 1 с tgstation/BandaStation (code/datums/martial):
/// CQC, Спящий карп, бокс, борьба, плазменный кулак, Каза Рук, корпоративное дзюдо и остальные.
/// </summary>
public sealed partial class MartialArtsSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private static readonly EntProtoId HelpAction = "ActionMartialArtsHelp";
    private static readonly EntProtoId SwapAction = "ActionMartialArtsSwap";
    private static readonly EntProtoId BlockAction = "ActionMartialArtsBlock";

    /// <summary>Adjacent(): приёмы работают только вплотную.</summary>
    private const float MeleeRange = 1.6f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MartialArtsComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MartialArtsComponent, MartialArtsHelpActionEvent>(OnHelpAction);
        SubscribeLocalEvent<MartialArtsComponent, MartialArtsSwapActionEvent>(OnSwapAction);
        SubscribeLocalEvent<MartialArtsComponent, MartialArtsBlockActionEvent>(OnBlockAction);
        SubscribeLocalEvent<MartialArtsComponent, MartialArtsMoveActionEvent>(OnMoveAction);

        // Удар (H)
        SubscribeLocalEvent<MartialArtsComponent, MeleeHitEvent>(OnUserMeleeHit);
        SubscribeLocalEvent<MartialArtGiverComponent, MeleeHitEvent>(OnGiverMeleeHit);

        // Толчок (D): ванильный толчок отменяется через AttemptMeleeEvent.
        SubscribeAllEvent<DisarmAttackEvent>(OnDisarmRequest, before: [typeof(MeleeWeaponSystem)]);
        SubscribeLocalEvent<MartialArtsComponent, AttemptMeleeEvent>(OnUserAttemptMelee);
        SubscribeLocalEvent<MartialArtGiverComponent, AttemptMeleeEvent>(OnGiverAttemptMelee);

        // Помощь (E): только дзюдо.
        SubscribeLocalEvent<MartialArtsComponent, UserInteractHandEvent>(OnUserInteractHand);

        // Блок
        SubscribeLocalEvent<MartialArtsComponent, AttackedEvent>(OnAttacked);
        SubscribeLocalEvent<MartialArtsComponent, DamageModifyEvent>(OnDamageModify);

        InitializeGrab();
        InitializeArts();
        InitializeItems();
    }

    #region Обучение

    /// <summary>teach(): выучить искусство. Источник — предмет, при снятии которого искусство забудется.</summary>
    public bool Teach(EntityUid user, ProtoId<MartialArtPrototype> art, EntityUid? source = null)
    {
        if (!_proto.TryIndex(art, out var proto) || !HasComp<MobStateComponent>(user))
            return false;

        if (!CanTeach(proto, user))
            return false;

        var comp = EnsureComp<MartialArtsComponent>(user);
        if (comp.Arts.Any(a => a.Art == art && a.Source == source))
            return false;

        var known = new KnownMartialArt { Art = art, Source = source };

        // Запертые стили всегда встают следующими в очередь.
        if (proto.LockedToUse && comp.Arts.Count >= 2)
            comp.Arts.Insert(1, known);
        else
            comp.Arts.Add(known);

        _actions.AddAction(user, ref comp.HelpAction, HelpAction);
        _actions.AddAction(user, ref comp.BlockAction, BlockAction);
        UpdateSwapAction(user, comp);

        if (comp.Arts.Count == 1)
        {
            ActivateStyle(user, comp, proto);
        }
        else if (comp.Arts.Count >= 2 && comp.Arts[^1] == known)
        {
            // Новое искусство сразу становится активным, если текущее не заперто.
            SwitchStyle(user, comp, comp.Arts.IndexOf(known));
        }

        return true;
    }

    /// <summary>can_teach().</summary>
    private bool CanTeach(MartialArtPrototype art, EntityUid user)
    {
        return art.Kind switch
        {
            MartialArtKind.Boxing or MartialArtKind.EvilBoxing or MartialArtKind.HunterBoxing => IsHuman(user),
            _ => true,
        };
    }

    /// <summary>unlearn(): забыть искусство, полученное от этого источника.</summary>
    public void Unlearn(EntityUid user, ProtoId<MartialArtPrototype> art, EntityUid? source = null)
    {
        if (!TryComp<MartialArtsComponent>(user, out var comp))
            return;

        var index = comp.Arts.FindIndex(a => a.Art == art && a.Source == source);
        if (index < 0)
            return;

        if (index == 0)
        {
            DeactivateStyle(user, comp);
            comp.Arts.RemoveAt(0);
            if (comp.Arts.Count > 0 && _proto.TryIndex(comp.Arts[0].Art, out var next))
                ActivateStyle(user, comp, next);
        }
        else
        {
            comp.Arts.RemoveAt(index);
        }

        if (comp.Arts.Count == 0)
        {
            RemComp<MartialArtsComponent>(user);
            return;
        }

        UpdateSwapAction(user, comp);
    }

    public bool Knows(EntityUid user, ProtoId<MartialArtPrototype> art)
    {
        return TryComp<MartialArtsComponent>(user, out var comp) && comp.Arts.Any(a => a.Art == art);
    }

    private void OnShutdown(Entity<MartialArtsComponent> ent, ref ComponentShutdown args)
    {
        DeactivateStyle(ent, ent.Comp);
        _actions.RemoveAction(ent.Owner, ent.Comp.HelpAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.SwapAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.BlockAction);
        StopGrab(ent, ent.Comp);
    }

    private void UpdateSwapAction(EntityUid user, MartialArtsComponent comp)
    {
        if (comp.Arts.Count >= 2)
            _actions.AddAction(user, ref comp.SwapAction, SwapAction);
        else
        {
            _actions.RemoveAction(user, comp.SwapAction);
            comp.SwapAction = null;
        }
    }

    /// <summary>activate_style().</summary>
    private void ActivateStyle(EntityUid user, MartialArtsComponent comp, MartialArtPrototype art)
    {
        foreach (var action in art.Actions)
        {
            if (_actions.AddAction(user, action) is { } granted)
                comp.StyleActions.Add(granted);
        }

        if (art.ActivateMessage is { } message)
            Notify(user, Loc.GetString(message));

        OnStyleActivated(user, comp, art);
    }

    /// <summary>deactivate_style().</summary>
    private void DeactivateStyle(EntityUid user, MartialArtsComponent comp)
    {
        if (comp.Active is not { } active || !_proto.TryIndex(active, out var art))
            return;

        foreach (var action in comp.StyleActions)
        {
            _actions.RemoveAction(user, action);
        }

        comp.StyleActions.Clear();
        ResetStreak(comp);

        if (art.DeactivateMessage is { } message && !TerminatingOrDeleted(user))
            Notify(user, Loc.GetString(message));

        OnStyleDeactivated(user, comp, art);
    }

    /// <summary>switch_style(): поставить искусство с индексом <paramref name="index"/> первым.</summary>
    private void SwitchStyle(EntityUid user, MartialArtsComponent comp, int index)
    {
        if (index <= 0 || index >= comp.Arts.Count)
            return;

        if (GetActive(comp) is { LockedToUse: true })
            return;

        DeactivateStyle(user, comp);
        var next = comp.Arts[index];
        comp.Arts.RemoveAt(index);
        comp.Arts.Insert(0, next);

        if (_proto.TryIndex(next.Art, out var art))
            ActivateStyle(user, comp, art);
    }

    private MartialArtPrototype? GetActive(MartialArtsComponent comp)
    {
        return comp.Active is { } id && _proto.TryIndex(id, out var art) ? art : null;
    }

    #endregion

    #region Кнопки

    /// <summary>swap_arts/Trigger(): справка по приёмам активного стиля.</summary>
    private void OnHelpAction(Entity<MartialArtsComponent> ent, ref MartialArtsHelpActionEvent args)
    {
        if (args.Handled || GetActive(ent.Comp) is not { } art)
            return;

        args.Handled = true;
        Notify(ent, art.Help is { } help
            ? Loc.GetString(help)
            : Loc.GetString("martial-no-help", ("art", Loc.GetString(art.Name))));
    }

    /// <summary>cycle_style().</summary>
    private void OnSwapAction(Entity<MartialArtsComponent> ent, ref MartialArtsSwapActionEvent args)
    {
        if (args.Handled || ent.Comp.Arts.Count < 2 || GetActive(ent.Comp) is not { } current)
            return;

        args.Handled = true;

        if (current.LockedToUse)
        {
            _popup.PopupEntity(Loc.GetString("martial-locked", ("art", Loc.GetString(current.Name))), ent, ent, PopupType.SmallCaution);
            return;
        }

        SwitchStyle(ent, ent.Comp, 1);
        if (GetActive(ent.Comp) is { } next)
            _popup.PopupEntity(Loc.GetString("martial-swapped", ("old", Loc.GetString(current.Name)), ("new", Loc.GetString(next.Name))), ent, ent);
    }

    /// <summary>throw_mode: в защитной стойке CQC, бокс и прочие блокируют удары.</summary>
    private void OnBlockAction(Entity<MartialArtsComponent> ent, ref MartialArtsBlockActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.BlockMode = !ent.Comp.BlockMode;
        _actions.SetToggled(ent.Comp.BlockAction, ent.Comp.BlockMode);
        _popup.PopupEntity(Loc.GetString(ent.Comp.BlockMode ? "martial-block-on" : "martial-block-off"), ent, ent);
    }

    #endregion

    #region Комбо

    /// <summary>add_to_streak().</summary>
    public void AddToStreak(EntityUid user, MartialArtsComponent comp, MartialArtPrototype art, string element, EntityUid target)
    {
        if (comp.StreakTarget != target)
            ResetStreak(comp, target);

        comp.Streak += element;
        if (comp.Streak.Length > art.MaxStreakLength)
            comp.Streak = comp.Streak[^art.MaxStreakLength..];

        if (!art.DisplayCombos)
            return;

        comp.StreakResetAt = _timing.CurTime + art.ComboTimer;
        _popup.PopupEntity(Loc.GetString("martial-combo", ("combo", FormatStreak(comp.Streak))), user, user);
    }

    /// <summary>reset_streak().</summary>
    public void ResetStreak(MartialArtsComponent comp, EntityUid? newTarget = null)
    {
        if (newTarget != comp.Restraining)
            comp.Restraining = null;

        comp.StreakTarget = newTarget;
        comp.Streak = string.Empty;
        comp.StreakResetAt = null;
    }

    /// <summary>Комбо-HUD TG: буквы приёмов, по-русски.</summary>
    private static string FormatStreak(string streak)
    {
        return string.Join("-", streak.Select(c => c switch
        {
            'H' => "У",
            'D' => "Т",
            'G' => "З",
            'E' => "П",
            _ => c.ToString(),
        }));
    }

    #endregion

    #region Ввод

    private void OnUserMeleeHit(Entity<MartialArtsComponent> ent, ref MeleeHitEvent args)
    {
        if (args.Weapon == ent.Owner)
            HandleHarm(ent, ref args);
    }

    /// <summary>Перчатки бокса в SS14 сами — оружие, поэтому удар в них тоже считается безоружным.</summary>
    private void OnGiverMeleeHit(Entity<MartialArtGiverComponent> ent, ref MeleeHitEvent args)
    {
        if (ent.Comp.Wearer is { } wearer && args.User == wearer && TryComp<MartialArtsComponent>(wearer, out var comp))
            HandleHarm((wearer, comp), ref args);
    }

    /// <summary>unarmed_strike() на harm-интенте.</summary>
    private void HandleHarm(Entity<MartialArtsComponent> ent, ref MeleeHitEvent args)
    {
        // Широкий замах не безоружный удар, а примеру с осмотром оружия делать нечего.
        if (!args.IsHit || args.Direction != null || args.HitEntities.Count != 1)
            return;

        var target = args.HitEntities[0];
        if (!IsValidTarget(ent, target) || GetActive(ent.Comp) is not { } art || !CanUse(ent, art))
            return;

        var result = HarmAct(ent, art, target);
        if (result != MartialResult.Invalid)
            args.Handled = true;
    }

    /// <summary>Толчок, который сейчас обрабатывает ванильная система: цель и тик запроса.</summary>
    private readonly Dictionary<EntityUid, (EntityUid Target, GameTick Tick)> _pendingDisarm = new();

    private void OnDisarmRequest(DisarmAttackEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user || GetEntity(ev.Target) is not { } target)
            return;

        _pendingDisarm[user] = (target, _timing.CurTick);
    }

    /// <summary>Забрать толчок, если он пришёл в этом же тике.</summary>
    private bool TryTakePendingDisarm(EntityUid user, out EntityUid target)
    {
        target = default;
        if (!_pendingDisarm.Remove(user, out var pending) || pending.Tick != _timing.CurTick)
            return false;

        target = pending.Target;
        return true;
    }

    private void OnUserAttemptMelee(Entity<MartialArtsComponent> ent, ref AttemptMeleeEvent args)
    {
        HandleAttemptMelee(ent, ref args);
    }

    private void OnGiverAttemptMelee(Entity<MartialArtGiverComponent> ent, ref AttemptMeleeEvent args)
    {
        if (ent.Comp.Wearer is { } wearer && TryComp<MartialArtsComponent>(wearer, out var comp))
            HandleAttemptMelee((wearer, comp), ref args);
    }

    /// <summary>
    /// unarmed_strike() на disarm-интенте. Толчок перехватывается до ванильного:
    /// если приём сработал — ванильный толчок отменяется.
    /// </summary>
    private void HandleAttemptMelee(Entity<MartialArtsComponent> ent, ref AttemptMeleeEvent args)
    {
        // changeNext_move() после хуков и апперкотов.
        if (_timing.CurTime < ent.Comp.NextAttackAllowed)
        {
            args.Cancelled = true;
            return;
        }

        if (!TryTakePendingDisarm(ent, out var target))
            return;

        if (!IsValidTarget(ent, target) || GetActive(ent.Comp) is not { } art || !CanUse(ent, art))
            return;

        var result = DisarmAct(ent, art, target);
        if (result != MartialResult.Invalid)
            args.Cancelled = true;
    }

    /// <summary>help_act(): клик пустой рукой без боевого режима.</summary>
    private void OnUserInteractHand(Entity<MartialArtsComponent> ent, ref UserInteractHandEvent args)
    {
        if (_combat.IsInCombatMode(ent) || !IsValidTarget(ent, args.Target))
            return;

        if (GetActive(ent.Comp) is not { } art || !CanUse(ent, art))
            return;

        HelpAct(ent, art, args.Target);
    }

    private bool IsValidTarget(EntityUid user, EntityUid target)
    {
        if (target == user || !HasComp<MobStateComponent>(target))
            return false;

        return _xform.InRange(user, target, MeleeRange);
    }

    #endregion

    #region Блок

    /// <summary>Запоминаем, кто бил нас в ближнем бою: урон от него через мгновение пройдёт через блок.</summary>
    private void OnAttacked(Entity<MartialArtsComponent> ent, ref AttackedEvent args)
    {
        ent.Comp.LastMeleeAttacker = args.User;
        ent.Comp.LastMeleeAttackAt = _timing.CurTime;
        _lastAttackUnarmed[ent.Owner] = args.Used == args.User || IsWornGloves(args.User, args.Used);
    }

    private readonly Dictionary<EntityUid, bool> _lastAttackUnarmed = new();

    private void OnDamageModify(Entity<MartialArtsComponent> ent, ref DamageModifyEvent args)
    {
        if (ent.Comp.LastMeleeAttacker is not { } attacker || args.Origin != attacker)
            return;

        if (_timing.CurTime - ent.Comp.LastMeleeAttackAt > TimeSpan.FromSeconds(0.1))
            return;

        ent.Comp.LastMeleeAttacker = null;
        var unarmed = _lastAttackUnarmed.Remove(ent.Owner, out var wasUnarmed) && wasUnarmed;
        var damage = args.Damage.GetTotal().Float();

        if (!CheckBlock(ent, attacker, damage, unarmed ? MartialAttackType.Unarmed : MartialAttackType.Melee))
            return;

        var zeroed = new DamageSpecifier();
        foreach (var key in args.Damage.DamageDict.Keys)
            zeroed.DamageDict[key] = FixedPoint2.Zero;
        args.Damage = zeroed;
    }

    /// <summary>check_block(): спрашивает искусство защитника, отбил ли он удар.</summary>
    public bool CheckBlock(EntityUid defender, EntityUid attacker, float damage, MartialAttackType type)
    {
        if (!TryComp<MartialArtsComponent>(defender, out var comp) || GetActive(comp) is not { } art)
            return false;

        if (!CanUse(defender, art))
            return false;

        return BlockAct((defender, comp), art, attacker, damage, type);
    }

    private bool IsWornGloves(EntityUid user, EntityUid item)
    {
        return _inventory.TryGetSlotEntity(user, "gloves", out var gloves) && gloves == item;
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<MartialArtsComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.StreakResetAt is { } reset && now >= reset)
                ResetStreak(comp);

            if (comp.Restraining != null && now >= comp.RestrainingUntil)
                comp.Restraining = null;

            UpdateGrab((uid, comp), now);
        }

        UpdateEffects(now);
    }

    private void Notify(EntityUid user, string message)
    {
        if (!_players.TryGetSessionByEntity(user, out var session))
            return;

        _chat.ChatMessageToOne(Shared.Chat.ChatChannel.Server, message, message, default, false, session.Channel);
    }
}

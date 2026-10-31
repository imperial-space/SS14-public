using System.Linq;
using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Server.Radio;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Отряды войны за Кадию. Командир через визор (или гололитический стол) создаёт отряды, раздаёт бойцов,
/// назначает командира отряда и отдаёт приказы «держать / наступать / отступить» — они приходят бойцам и
/// ложатся на голокарту. Вокс-команды офицера дают бойцам рядом временный бонус. У отряда свой вокс.
/// Комиссар воодушевляет бойцов и казнит трусов.
/// </summary>
public sealed class CadiaSquadSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly CadiaHoloTableSystem _holo = default!;

    public const int MaxSquads = 8;
    private const int AutoSquadSize = 10;
    private static readonly ProtoId<Content.Shared.Damage.Prototypes.DamageTypePrototype> PiercingDamage = "Piercing";
    private static readonly TimeSpan BuffTime = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan VoxCooldown = TimeSpan.FromSeconds(90);
    private const float VoxRange = 14f;
    private static readonly TimeSpan UiRate = TimeSpan.FromSeconds(1);

    public static readonly Color[] Colors =
    {
        Color.FromHex("#ff4a3a"), Color.FromHex("#4ab8ff"), Color.FromHex("#ffd37a"), Color.FromHex("#5aff78"),
        Color.FromHex("#c66bff"), Color.FromHex("#ff9a3c"), Color.FromHex("#3dffc8"), Color.FromHex("#ffffff"),
    };

    private static readonly SoundSpecifier OrderSound = new SoundPathSpecifier("/Audio/Items/Whistle/trench_whistle_1.ogg");
    private static readonly SoundSpecifier HoldSound = new SoundPathSpecifier("/Audio/Machines/twobeep.ogg");
    private static readonly SoundSpecifier VoxSound = new SoundPathSpecifier("/Audio/Items/Whistle/trench_whistle_2.ogg");
    [Dependency] private readonly CadiaMoraleSystem _morale = default!;

    private static readonly SoundSpecifier InspireSound = new SoundPathSpecifier("/Audio/Misc/notice2.ogg");
    private static readonly SoundSpecifier ExecuteSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Gunshots/hpistol.ogg", AudioParams.Default.WithVolume(4));

    private sealed class Squad
    {
        public int Number;
        public string Name = string.Empty;
        public Color Color;
        public EntityUid? Leader;
        public CadiaSquadOrder Order;
        public Vector2 OrderPoint;
        public CadiaFlightLevel OrderLevel;
        public bool Astartes;
        public readonly HashSet<EntityUid> Members = new();
        public readonly List<int> OrderElements = new();
    }

    private readonly Dictionary<CadiaFaction, Dictionary<int, Squad>> _squads = new()
    {
        [CadiaFaction.Imperium] = new(),
        [CadiaFaction.Chaos] = new(),
    };
    private readonly Dictionary<EntityUid, TimeSpan> _voxCooldown = new();
    private readonly List<(EntityUid Mob, TimeSpan At)> _expiries = new();
    private TimeSpan _nextUi;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CadiaHoloTableComponent, CadiaSquadMenuActionEvent>(OnMenuAction);
        SubscribeLocalEvent<CadiaHoloTableComponent, GetVerbsEvent<AlternativeVerb>>(OnTableVerbs);
        Subs.BuiEvents<CadiaHoloTableComponent>(CadiaSquadUiKey.Key, subs =>
        {
            subs.Event<CadiaSquadCreateMessage>(OnCreate);
            subs.Event<CadiaSquadDisbandMessage>(OnDisband);
            subs.Event<CadiaSquadAssignMessage>(OnAssign);
            subs.Event<CadiaSquadLeaderMessage>(OnLeader);
            subs.Event<CadiaSquadOrderMessage>(OnOrder);
            subs.Event<CadiaSquadVoxMessage>(OnVox);
        });
        Subs.BuiEvents<CadiaHoloTableComponent>(CadiaHoloUiKey.Key, subs =>
        {
            subs.Event<CadiaHoloOrderMessage>(OnHoloOrder);
        });

        SubscribeLocalEvent<RadioReceiveAttemptEvent>(OnRadioReceive);
        SubscribeLocalEvent<CadiaSquadBuffComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<CadiaSuppressedGunComponent, GunRefreshModifiersEvent>(OnGunRefresh);

        SubscribeLocalEvent<CadiaCommissarComponent, CadiaInspireActionEvent>(OnInspire);
        SubscribeLocalEvent<CadiaCommissarComponent, CadiaExecuteActionEvent>(OnExecute);
        SubscribeLocalEvent<CadiaFactionMemberComponent, CadiaExecuteDoAfterEvent>(OnExecuted);
    }

    // ═══════════════════════ Данные для других систем ═══════════════════════

    public IEnumerable<CadiaHoloSquad> HoloSquads(CadiaFaction faction)
    {
        return _squads[faction].Values.OrderBy(s => s.Number).Select(s => new CadiaHoloSquad(s.Number, s.Name, s.Color));
    }

    // ═══════════════════════ Окно ═══════════════════════

    /// <summary>У стола (не визора) — пункт меню «Отряды».</summary>
    private void OnTableVerbs(Entity<CadiaHoloTableComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.ReadOnly)
            return;
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cadia-squad-verb"),
            Priority = 3,
            Act = () =>
            {
                if (_ui.TryOpenUi(ent.Owner, CadiaSquadUiKey.Key, user))
                    UpdateUi(ent);
            },
        });
    }

    private void OnMenuAction(Entity<CadiaHoloTableComponent> ent, ref CadiaSquadMenuActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        if (_ui.IsUiOpen(ent.Owner, CadiaSquadUiKey.Key, args.Performer))
            _ui.CloseUi(ent.Owner, CadiaSquadUiKey.Key, args.Performer);
        else if (_ui.TryOpenUi(ent.Owner, CadiaSquadUiKey.Key, args.Performer))
            UpdateUi(ent);
    }

    private void UpdateUi(Entity<CadiaHoloTableComponent> ent)
    {
        var faction = ent.Comp.Faction;
        var state = new CadiaSquadState { Faction = faction };
        foreach (var squad in _squads[faction].Values.OrderBy(s => s.Number))
        {
            Prune(squad);
            state.Squads.Add(new CadiaSquadInfo
            {
                Number = squad.Number,
                Name = squad.Name,
                Color = squad.Color,
                Leader = squad.Leader is { } leader ? GetNetEntity(leader) : null,
                Order = squad.Order,
                Members = squad.Members.Select(m => GetNetEntity(m)).ToList(),
            });
        }

        var origin = _xform.GetWorldPosition(ent);
        var map = Transform(ent).MapUid;
        var members = EntityQueryEnumerator<CadiaFactionMemberComponent, TransformComponent>();
        while (members.MoveNext(out var uid, out var member, out var xform))
        {
            if (member.Role is not { } role || member.Faction != faction || !_proto.TryIndex(role, out var proto))
                continue;
            var squad = CompOrNull<CadiaSquadMemberComponent>(uid)?.Number ?? 0;
            var distance = xform.MapUid == map ? (_xform.GetWorldPosition(xform) - origin).Length() : -1f;
            state.Fighters.Add(new CadiaSquadFighter(GetNetEntity(uid), Name(uid), Loc.GetString(proto.Name), squad, !_mobState.IsDead(uid), distance));
        }
        state.Fighters.Sort((a, b) => string.CompareOrdinal(a.Role + a.Name, b.Role + b.Name));

        foreach (var actor in _ui.GetActors(ent.Owner, CadiaSquadUiKey.Key).ToList())
        {
            state.VoxCooldownUntil = _voxCooldown.GetValueOrDefault(actor);
            _ui.SetUiState(ent.Owner, CadiaSquadUiKey.Key, state);
        }
    }

    private void UpdateFaction(CadiaFaction faction)
    {
        var query = EntityQueryEnumerator<CadiaHoloTableComponent>();
        while (query.MoveNext(out var uid, out var table))
        {
            if (table.Faction == faction && _ui.IsUiOpen(uid, CadiaSquadUiKey.Key))
                UpdateUi((uid, table));
        }
    }

    private bool IsCommander(EntityUid user, CadiaFaction faction)
    {
        return TryComp<CadiaFactionMemberComponent>(user, out var member) && member.Faction == faction &&
               member.Role is { } role && _proto.TryIndex(role, out var proto) && proto.Commander;
    }

    private bool Deny(EntityUid user, CadiaFaction faction)
    {
        if (IsCommander(user, faction))
            return false;
        _popup.PopupEntity(Loc.GetString("cadia-squad-commander-only"), user, user, PopupType.SmallCaution);
        return true;
    }

    // ═══════════════════════ Состав ═══════════════════════

    private void OnCreate(Entity<CadiaHoloTableComponent> ent, ref CadiaSquadCreateMessage args)
    {
        var faction = ent.Comp.Faction;
        if (Deny(args.Actor, faction))
            return;
        var squads = _squads[faction];
        var number = Enumerable.Range(1, MaxSquads).FirstOrDefault(n => !squads.ContainsKey(n));
        if (number == 0)
        {
            _popup.PopupEntity(Loc.GetString("cadia-squad-max"), args.Actor, args.Actor);
            return;
        }
        var name = args.Name.Trim();
        if (name.Length > 24)
            name = name[..24];
        squads[number] = new Squad
        {
            Number = number,
            Name = string.IsNullOrEmpty(name) ? Loc.GetString("cadia-squad-default-name", ("number", number)) : name,
            Color = Colors[number - 1],
        };
        _audio.PlayPvs(HoldSound, ent);
        UpdateFaction(faction);
        _holo.RefreshFaction(faction);
    }

    /// <summary>
    /// Новый боец сразу попадает в отряд своего рода войск с наименьшим числом живых: без этого он бродит один и не слышит приказов.
    /// Сержант ведёт отряд без живого командира, а если такого нет, получает новый.
    /// </summary>
    public void AutoAssign(EntityUid mob, CadiaFaction faction, bool leader)
    {
        var astartes = HasComp<CadiaAstartesComponent>(mob);
        var squads = _squads[faction];
        Squad? best = null;
        var bestAlive = int.MaxValue;
        foreach (var squad in squads.Values)
        {
            Prune(squad);
            if (squad.Astartes != astartes)
                continue;
            if (leader && squad.Leader is { } current && !_mobState.IsDead(current))
                continue;
            var alive = squad.Members.Count(m => !_mobState.IsDead(m));
            if (alive < bestAlive)
            {
                best = squad;
                bestAlive = alive;
            }
        }

        var created = false;
        if (best == null || !leader && bestAlive >= AutoSquadSize)
        {
            var number = Enumerable.Range(1, MaxSquads).FirstOrDefault(n => !squads.ContainsKey(n));
            if (number != 0)
            {
                best = new Squad
                {
                    Number = number,
                    Name = Loc.GetString(astartes ? "cadia-squad-auto-astartes" : "cadia-squad-default-name", ("number", number)),
                    Color = Colors[number - 1],
                    Astartes = astartes,
                };
                squads[number] = best;
                created = true;
            }
        }
        // Все восемь заняты: остаётся самый малочисленный, даже если он чужого рода войск.
        best ??= squads.Values.MinBy(s => s.Members.Count);
        if (best == null)
            return;

        foreach (var squad in squads.Values)
            squad.Members.Remove(mob);
        best.Members.Add(mob);
        if (leader)
            best.Leader = mob;
        Sync(best, faction);

        var leaderName = best.Leader is { } head && head != mob ? Name(head) : null;
        var text = leaderName != null
            ? Loc.GetString("cadia-squad-auto-joined-leader", ("name", best.Name), ("leader", leaderName))
            : Loc.GetString(leader ? "cadia-squad-now-leader" : "cadia-squad-joined", ("name", best.Name));
        _popup.PopupEntity(text, mob, mob, PopupType.Medium);
        UpdateFaction(faction);
        if (created)
            _holo.RefreshFaction(faction);
    }

    private void OnDisband(Entity<CadiaHoloTableComponent> ent, ref CadiaSquadDisbandMessage args)
    {
        var faction = ent.Comp.Faction;
        if (Deny(args.Actor, faction) || !_squads[faction].Remove(args.Number, out var squad))
            return;
        foreach (var member in squad.Members)
            RemCompDeferred<CadiaSquadMemberComponent>(member);
        foreach (var id in squad.OrderElements)
            _holo.RemoveElement(faction, id);
        UpdateFaction(faction);
        _holo.RefreshFaction(faction);
    }

    private void OnAssign(Entity<CadiaHoloTableComponent> ent, ref CadiaSquadAssignMessage args)
    {
        var faction = ent.Comp.Faction;
        if (Deny(args.Actor, faction))
            return;
        var fighter = GetEntity(args.Fighter);
        if (!TryComp<CadiaFactionMemberComponent>(fighter, out var member) || member.Faction != faction)
            return;

        foreach (var squad in _squads[faction].Values)
        {
            squad.Members.Remove(fighter);
            if (squad.Leader == fighter)
                squad.Leader = null;
        }

        if (args.Number == 0 || !_squads[faction].TryGetValue(args.Number, out var target))
        {
            RemComp<CadiaSquadMemberComponent>(fighter);
        }
        else
        {
            target.Members.Add(fighter);
            Sync(target, faction);
            _popup.PopupEntity(Loc.GetString("cadia-squad-joined", ("name", target.Name)), fighter, fighter, PopupType.Medium);
        }
        UpdateFaction(faction);
    }

    private void OnLeader(Entity<CadiaHoloTableComponent> ent, ref CadiaSquadLeaderMessage args)
    {
        var faction = ent.Comp.Faction;
        if (Deny(args.Actor, faction))
            return;
        var fighter = GetEntity(args.Fighter);
        var squad = _squads[faction].Values.FirstOrDefault(s => s.Members.Contains(fighter));
        if (squad == null)
            return;
        squad.Leader = fighter;
        Sync(squad, faction);
        _popup.PopupEntity(Loc.GetString("cadia-squad-now-leader", ("name", squad.Name)), fighter, fighter, PopupType.Large);
        UpdateFaction(faction);
    }

    /// <summary>Знак отряда на бойцах: номер, цвет, название, командир, приказ.</summary>
    private void Sync(Squad squad, CadiaFaction faction)
    {
        Prune(squad);
        foreach (var member in squad.Members)
        {
            var comp = EnsureComp<CadiaSquadMemberComponent>(member);
            comp.Number = squad.Number;
            comp.Name = squad.Name;
            comp.Color = squad.Color;
            comp.Leader = squad.Leader == member;
            comp.Faction = faction;
            comp.Order = squad.Order;
            comp.OrderPoint = squad.OrderPoint;
            comp.OrderLevel = squad.OrderLevel;
            Dirty(member, comp);
        }
    }

    private void Prune(Squad squad)
    {
        squad.Members.RemoveWhere(m => TerminatingOrDeleted(m));
        if (squad.Leader is { } leader && !squad.Members.Contains(leader))
            squad.Leader = null;
    }

    /// <summary>Номер отряда бойца (0 — без отряда).</summary>
    public int SquadOf(EntityUid mob)
    {
        return CompOrNull<CadiaSquadMemberComponent>(mob)?.Number ?? 0;
    }

    // ═══════════════════════ Приказы ═══════════════════════

    private void OnOrder(Entity<CadiaHoloTableComponent> ent, ref CadiaSquadOrderMessage args)
    {
        var faction = ent.Comp.Faction;
        if (Deny(args.Actor, faction) || !_squads[faction].TryGetValue(args.Number, out var squad))
            return;
        // Без точки на карте — приказ «здесь»: у командира отряда, а если его нет — у центра отряда.
        var anchor = squad.Leader ?? squad.Members.FirstOrDefault();
        var point = anchor.IsValid() ? _xform.GetWorldPosition(anchor) : _xform.GetWorldPosition(ent);
        var level = CompOrNull<CadiaFlightLevelComponent>(Transform(anchor.IsValid() ? anchor : ent.Owner).MapUid)?.Level ?? CadiaFlightLevel.Surface;
        IssueOrder(faction, squad, args.Order, point, level, args.Actor);
    }

    private void OnHoloOrder(Entity<CadiaHoloTableComponent> ent, ref CadiaHoloOrderMessage args)
    {
        var faction = ent.Comp.Faction;
        if (ent.Comp.ReadOnly || Deny(args.Actor, faction) || !_squads[faction].TryGetValue(args.Squad, out var squad))
            return;
        IssueOrder(faction, squad, args.Order, args.Point, args.Level, args.Actor);
    }

    /// <summary>Приказ: бойцам — свисток и крупная надпись, на голокарту — стрелка или круг цветом отряда.</summary>
    private void IssueOrder(CadiaFaction faction, Squad squad, CadiaSquadOrder order, Vector2 point, CadiaFlightLevel level, EntityUid commander)
    {
        Prune(squad);
        squad.Order = order;
        squad.OrderPoint = point;
        squad.OrderLevel = level;
        foreach (var id in squad.OrderElements)
            _holo.RemoveElement(faction, id);
        squad.OrderElements.Clear();

        var key = order.ToString().ToLowerInvariant();
        var label = Loc.GetString("cadia-squad-order-label", ("number", squad.Number), ("order", Loc.GetString($"cadia-squad-order-{key}")));
        if (order != CadiaSquadOrder.None)
        {
            var centroid = squad.Members.Count > 0
                ? squad.Members.Select(m => _xform.GetWorldPosition(m)).Aggregate(Vector2.Zero, (a, b) => a + b) / squad.Members.Count
                : point;
            CadiaHoloElement shape = order == CadiaSquadOrder.Hold
                ? new CadiaHoloElement { Shape = CadiaHoloShape.Circle, A = point, B = point + new Vector2(3, 0) }
                : new CadiaHoloElement { Shape = CadiaHoloShape.Arrow, A = (centroid - point).Length() > 1f ? centroid : point - new Vector2(0, 4), B = point };
            shape.Layer = CadiaHoloLayer.Assault;
            shape.Level = level;
            shape.Color = squad.Color;
            squad.OrderElements.Add(_holo.AddSystemElement(faction, shape, Name(commander)));
            squad.OrderElements.Add(_holo.AddSystemElement(faction, new CadiaHoloElement
            {
                Shape = CadiaHoloShape.Text,
                Layer = CadiaHoloLayer.Assault,
                Level = level,
                Color = squad.Color,
                A = point + new Vector2(1, 1.5f),
                B = point,
                Text = label,
            }, Name(commander)));
        }

        Sync(squad, faction);
        var text = Loc.GetString($"cadia-squad-order-popup-{key}", ("name", squad.Name));
        foreach (var member in squad.Members)
        {
            _popup.PopupEntity(text, member, member, PopupType.LargeCaution);
            _audio.PlayEntity(order == CadiaSquadOrder.Advance ? OrderSound : HoldSound, Filter.Entities(member), member, false);
        }
        // Командир в своём же отряде уже видит сам приказ, вторая надпись ни к чему.
        if (!squad.Members.Contains(commander))
            _popup.PopupEntity(Loc.GetString("cadia-squad-order-sent", ("name", squad.Name)), commander, commander);
        UpdateFaction(faction);
    }

    // ═══════════════════════ Вокс-команды ═══════════════════════

    private void OnVox(Entity<CadiaHoloTableComponent> ent, ref CadiaSquadVoxMessage args)
    {
        var faction = ent.Comp.Faction;
        var user = args.Actor;
        if (Deny(user, faction) || !_squads[faction].TryGetValue(args.Number, out var squad))
            return;

        var now = _timing.CurTime;
        if (_voxCooldown.TryGetValue(user, out var until) && now < until)
        {
            _popup.PopupEntity(Loc.GetString("cadia-squad-vox-cooldown", ("seconds", (int) (until - now).TotalSeconds)), user, user);
            return;
        }

        var key = args.Command.ToString().ToLowerInvariant();
        _chat.TrySendInGameICMessage(user, Loc.GetString($"cadia-squad-vox-shout-{key}"), InGameICChatType.Speak, false);
        _audio.PlayPvs(VoxSound, user);
        _voxCooldown[user] = now + VoxCooldown;

        var origin = _xform.GetMapCoordinates(user);
        var affected = 0;
        foreach (var member in squad.Members)
        {
            var pos = _xform.GetMapCoordinates(member);
            if (pos.MapId != origin.MapId || (pos.Position - origin.Position).Length() > VoxRange || !_mobState.IsAlive(member))
                continue;
            ApplyBuff(member, args.Command);
            _popup.PopupEntity(Loc.GetString($"cadia-squad-vox-buff-{key}"), member, member, PopupType.Large);
            affected++;
        }
        _popup.PopupEntity(Loc.GetString("cadia-squad-vox-done", ("count", affected)), user, user);
        UpdateFaction(faction);
    }

    private void ApplyBuff(EntityUid mob, CadiaVoxCommand command)
    {
        var buff = EnsureComp<CadiaSquadBuffComponent>(mob);
        var until = _timing.CurTime + BuffTime;
        switch (command)
        {
            case CadiaVoxCommand.Suppress:
                buff.SuppressUntil = until;
                RefreshGuns(mob);
                break;
            case CadiaVoxCommand.Hold:
                buff.HoldUntil = until;
                break;
            case CadiaVoxCommand.Charge:
                buff.ChargeUntil = until;
                _speed.RefreshMovementSpeedModifiers(mob);
                break;
        }
        Dirty(mob, buff);
        _expiries.Add((mob, until));
    }

    /// <summary>Оружие в руках бойца под «огнём на подавление» стреляет кучнее.</summary>
    private void RefreshGuns(EntityUid mob)
    {
        var active = TryComp<CadiaSquadBuffComponent>(mob, out var buff) && _timing.CurTime < buff.SuppressUntil;
        foreach (var held in _hands.EnumerateHeld(mob))
        {
            if (!HasComp<GunComponent>(held))
                continue;
            if (active)
                EnsureComp<CadiaSuppressedGunComponent>(held);
            else
                RemComp<CadiaSuppressedGunComponent>(held);
            _gun.RefreshModifiers(held);
        }
    }

    private void OnGunRefresh(Entity<CadiaSuppressedGunComponent> ent, ref GunRefreshModifiersEvent args)
    {
        args.MinAngle *= 0.5;
        args.MaxAngle *= 0.5;
        args.AngleIncrease *= 0.5;
    }

    private void OnDamageModify(Entity<CadiaSquadBuffComponent> ent, ref DamageModifyEvent args)
    {
        if (_timing.CurTime < ent.Comp.HoldUntil)
            args.Damage *= 0.75f;
    }

    // ═══════════════════════ Комиссар ═══════════════════════

    private void OnInspire(Entity<CadiaCommissarComponent> ent, ref CadiaInspireActionEvent args)
    {
        if (args.Handled || !SameSide(args.Performer, args.Target) || !_mobState.IsAlive(args.Target))
            return;
        args.Handled = true;
        ApplyBuff(args.Target, CadiaVoxCommand.Hold);
        ApplyBuff(args.Target, CadiaVoxCommand.Charge);
        _morale.Change(args.Target, 50f);
        _chat.TrySendInGameICMessage(args.Performer, Loc.GetString("cadia-commissar-inspire-shout"), InGameICChatType.Speak, false);
        _audio.PlayPvs(InspireSound, args.Target);
        _popup.PopupEntity(Loc.GetString("cadia-commissar-inspired"), args.Target, args.Target, PopupType.Large);
    }

    private void OnExecute(Entity<CadiaCommissarComponent> ent, ref CadiaExecuteActionEvent args)
    {
        if (args.Handled || args.Target == args.Performer || !SameSide(args.Performer, args.Target) || _mobState.IsDead(args.Target))
            return;
        args.Handled = true;

        var doAfter = new DoAfterArgs(EntityManager, args.Performer, TimeSpan.FromSeconds(2.5), new CadiaExecuteDoAfterEvent(), args.Target, args.Target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 2f,
        };
        if (!_doAfter.TryStartDoAfter(doAfter))
            return;
        _chat.TrySendInGameICMessage(args.Performer, Loc.GetString("cadia-commissar-execute-shout", ("name", Name(args.Target))), InGameICChatType.Speak, false);
        _popup.PopupEntity(Loc.GetString("cadia-commissar-execute-start", ("commissar", Name(args.Performer))), args.Target, args.Target, PopupType.LargeCaution);
    }

    private void OnExecuted(Entity<CadiaFactionMemberComponent> ent, ref CadiaExecuteDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;
        args.Handled = true;

        _audio.PlayPvs(ExecuteSound, ent);
        _damage.TryChangeDamage(ent.Owner, new DamageSpecifier(_proto.Index(PiercingDamage), 400), true, origin: args.User);
        _popup.PopupCoordinates(Loc.GetString("cadia-commissar-executed", ("name", Name(ent))), Transform(ent).Coordinates, PopupType.LargeCaution);

        // Показательная казнь: свои бойцы рядом стоят насмерть.
        var origin = _xform.GetMapCoordinates(ent);
        var members = EntityQueryEnumerator<CadiaFactionMemberComponent, TransformComponent>();
        while (members.MoveNext(out var uid, out var member, out var xform))
        {
            if (uid == ent.Owner || member.Faction != ent.Comp.Faction || !_mobState.IsAlive(uid) || xform.MapID != origin.MapId ||
                (_xform.GetWorldPosition(xform) - origin.Position).Length() > 10f)
                continue;
            ApplyBuff(uid, CadiaVoxCommand.Hold);
            _morale.Change(uid, 25f);
            _popup.PopupEntity(Loc.GetString("cadia-commissar-witness"), uid, uid, PopupType.Medium);
        }
    }

    private bool SameSide(EntityUid a, EntityUid b)
    {
        return TryComp<CadiaFactionMemberComponent>(a, out var ma) && TryComp<CadiaFactionMemberComponent>(b, out var mb) &&
               ma.Role != null && mb.Role != null && ma.Faction == mb.Faction;
    }

    // ═══════════════════════ Вокс отряда ═══════════════════════

    /// <summary>Вокс отряда слышат только бойцы того же отряда.</summary>
    private void OnRadioReceive(ref RadioReceiveAttemptEvent args)
    {
        if (args.Channel.ID != "CadiaSquad" && args.Channel.ID != "CadiaWarbandSquad")
            return;
        var sender = Transform(args.RadioSource).ParentUid;
        var receiver = Transform(args.RadioReceiver).ParentUid;
        var senderSquad = CompOrNull<CadiaSquadMemberComponent>(sender);
        var receiverSquad = CompOrNull<CadiaSquadMemberComponent>(receiver);
        if (senderSquad == null || receiverSquad == null || senderSquad.Number != receiverSquad.Number || senderSquad.Faction != receiverSquad.Faction)
            args.Cancelled = true;
    }

    // ═══════════════════════ Обновление ═══════════════════════

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        // Бонусы истекли — пересчитать скорость и оружие.
        for (var i = _expiries.Count - 1; i >= 0; i--)
        {
            var (mob, at) = _expiries[i];
            if (now < at)
                continue;
            _expiries.RemoveAt(i);
            if (TerminatingOrDeleted(mob))
                continue;
            _speed.RefreshMovementSpeedModifiers(mob);
            RefreshGuns(mob);
        }

        if (now < _nextUi)
            return;
        _nextUi = now + UiRate;
        UpdateFaction(CadiaFaction.Imperium);
        UpdateFaction(CadiaFaction.Chaos);
    }
}

/// <summary>Оружие бойца под «огнём на подавление».</summary>
[RegisterComponent]
public sealed partial class CadiaSuppressedGunComponent : Component;

/// <summary>Фуражка комиссара: даёт действия «Воодушевить» и «Казнь».</summary>
[RegisterComponent]
public sealed partial class CadiaCommissarComponent : Component;

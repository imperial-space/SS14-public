using System.Linq;
using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>Итоги войны: кто чем отличился, плюс медали, которые офицеры вручают прямо в бою.</summary>
/// <summary>События войны, которые попадают в итоги числом.</summary>
public enum CadiaWarCount : byte
{
    PodsLanded,
    PodsShotDown,
    RefugeesSaved,
    RefugeesDied,
    RitualsChaos,
    RitualsImperium,
    Boardings,
    BerthsWrecked,
}

public sealed class CadiaStatsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly Content.Server.Chat.Managers.IChatManager _chatManager = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly CadiaDefenseRuleSystem _rule = default!;

    private static readonly TimeSpan KillCredit = TimeSpan.FromSeconds(30);
    private const float TeamDamageWarnStep = 50f;
    private static readonly EntProtoId MedalImperium = "CadiaMedalImperium";
    private static readonly EntProtoId MedalChaos = "CadiaMedalChaos";
    private static readonly EntProtoId MedalAction = "ActionCadiaAwardMedal";
    private static readonly SoundSpecifier MedalSound = new SoundPathSpecifier("/Audio/Misc/notice2.ogg");

    private sealed class PlayerStat
    {
        public string Name = string.Empty;
        public int Kills;
        public int Pylons;
        public float Healed;
        public int Deaths;
        public TimeSpan BestLife;
        public TimeSpan? LifeStart;
        public int TeamKills;
        public float TeamDamage;
        public int WarnedDamageSteps;
    }

    private sealed class VehicleStat
    {
        public string Name = string.Empty;
        public int Kills;
        public readonly HashSet<string> Crew = new();
    }

    private readonly Dictionary<NetUserId, PlayerStat> _players = new();
    private readonly Dictionary<EntityUid, VehicleStat> _vehicles = new();
    private readonly Dictionary<EntityUid, (EntityUid Attacker, TimeSpan At)> _lastHit = new();
    private readonly Dictionary<EntityUid, Dictionary<NetUserId, float>> _pylonDamage = new();
    private readonly List<string> _medals = new();
    /// <summary>Счётчики войны для итогов: капсулы, ритуалы, абордажи, горожане.</summary>
    private readonly Dictionary<CadiaWarCount, int> _counts = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CadiaFactionMemberComponent, DamageChangedEvent>(OnMemberDamaged);
        SubscribeLocalEvent<CadiaPylonComponent, DamageChangedEvent>(OnPylonDamaged);
        SubscribeLocalEvent<CadiaMedalGiverComponent, CadiaAwardMedalEvent>(OnAwardMedal);
    }

    public void Reset()
    {
        _players.Clear();
        _vehicles.Clear();
        _lastHit.Clear();
        _pylonDamage.Clear();
        _medals.Clear();
        _counts.Clear();
    }

    /// <summary>Отметить событие войны для итогов.</summary>
    public void Count(CadiaWarCount what, int amount = 1)
    {
        _counts[what] = _counts.GetValueOrDefault(what) + amount;
    }

    // ═══════════════════════ Учёт ═══════════════════════

    private PlayerStat Stat(NetUserId user)
    {
        if (!_players.TryGetValue(user, out var stat))
            _players[user] = stat = new PlayerStat();
        return stat;
    }

    /// <summary>Игрок вошёл в тело бойца: начинается отсчёт этой жизни.</summary>
    public void Joined(ICommonSession session, EntityUid mob)
    {
        var stat = Stat(session.UserId);
        stat.Name = $"{Name(mob)} ({session.Name})";
        stat.LifeStart = _timing.CurTime;
    }

    private bool TryPlayer(EntityUid? uid, out NetUserId user)
    {
        user = default;
        if (uid is not { } ent || !TryComp<ActorComponent>(ent, out var actor))
            return false;
        user = actor.PlayerSession.UserId;
        return true;
    }

    private void OnMemberDamaged(Entity<CadiaFactionMemberComponent> ent, ref DamageChangedEvent args)
    {
        if (args.Origin is not { } origin || origin == ent.Owner || args.DamageDelta is not { } delta)
            return;

        if (args.DamageIncreased)
        {
            _lastHit[ent] = (origin, _timing.CurTime);
            RecordFriendlyFire(ent, origin, (float) delta.GetTotal());
            return;
        }

        // Лечение: считаем только то, что наложил другой боец.
        var healed = -(float) delta.GetTotal();
        if (healed > 0f && TryPlayer(origin, out var healer))
            Stat(healer).Healed += healed;
    }

    /// <summary>Огонь по своим: ведущий видит счёт на панели, стрелок получает предупреждение.</summary>
    private void RecordFriendlyFire(Entity<CadiaFactionMemberComponent> victim, EntityUid origin, float damage)
    {
        if (damage <= 0f || !TryPlayer(origin, out var user) ||
            !TryComp<CadiaFactionMemberComponent>(origin, out var attacker) || attacker.Faction != victim.Comp.Faction)
            return;
        var stat = Stat(user);
        stat.TeamDamage += damage;
        var steps = (int) (stat.TeamDamage / TeamDamageWarnStep);
        if (steps <= stat.WarnedDamageSteps)
            return;
        stat.WarnedDamageSteps = steps;
        _popup.PopupEntity(Loc.GetString("cadia-ff-warning"), origin, origin, PopupType.LargeCaution);
    }

    public (int Kills, int Damage) TeamRecord(NetUserId user)
    {
        return _players.TryGetValue(user, out var stat) ? (stat.TeamKills, (int) stat.TeamDamage) : (0, 0);
    }

    private void OnPylonDamaged(Entity<CadiaPylonComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta is not { } delta || !TryPlayer(args.Origin, out var user))
            return;
        if (!_pylonDamage.TryGetValue(ent, out var damage))
            _pylonDamage[ent] = damage = new Dictionary<NetUserId, float>();
        damage[user] = damage.GetValueOrDefault(user) + (float) delta.GetTotal();
    }

    /// <summary>Пилон пал: честь тому, кто нанёс больше всего урона.</summary>
    public void PylonFell(EntityUid pylon)
    {
        if (!_pylonDamage.Remove(pylon, out var damage) || damage.Count == 0)
            return;
        Stat(damage.MaxBy(pair => pair.Value).Key).Pylons++;
    }

    /// <summary>Боец погиб: зачесть смерть ему и убийство последнему, кто его ранил, если это был враг.</summary>
    public void MemberDied(EntityUid mob, CadiaFactionMemberComponent member)
    {
        var now = _timing.CurTime;
        if (TryPlayer(mob, out var victim))
        {
            var stat = Stat(victim);
            stat.Deaths++;
            CloseLife(stat, now);
        }

        if (!_lastHit.Remove(mob, out var hit) || now - hit.At > KillCredit)
            return;
        if (TryComp<CadiaFactionMemberComponent>(hit.Attacker, out var attacker) && attacker.Faction == member.Faction)
        {
            if (TryPlayer(hit.Attacker, out var traitor))
            {
                var stat = Stat(traitor);
                stat.TeamKills++;
                _popup.PopupEntity(Loc.GetString("cadia-ff-kill"), hit.Attacker, hit.Attacker, PopupType.LargeCaution);
                // Ведущий узнаёт сразу, а не только при взгляде на панель.
                _chatManager.SendAdminAlert(Loc.GetString("cadia-ff-admin", ("name", stat.Name), ("victim", Name(mob)), ("kills", stat.TeamKills)));
            }
            return;
        }

        if (TryPlayer(hit.Attacker, out var killer))
            Stat(killer).Kills++;
        if (TryComp<CadiaVehicleCrewComponent>(hit.Attacker, out var crew))
            CreditVehicle(crew.Vehicle);
        else if (HasComp<CadiaVehicleComponent>(hit.Attacker))
            CreditVehicle(hit.Attacker);
    }

    private void CreditVehicle(EntityUid vehicle)
    {
        if (!TryComp<CadiaVehicleComponent>(vehicle, out var comp))
            return;
        if (!_vehicles.TryGetValue(vehicle, out var stat))
            _vehicles[vehicle] = stat = new VehicleStat { Name = Name(vehicle) };
        stat.Kills++;
        if (comp.Driver is { } driver)
            stat.Crew.Add(Name(driver));
        if (comp.Gunner is { } gunner)
            stat.Crew.Add(Name(gunner));
    }

    private static void CloseLife(PlayerStat stat, TimeSpan now)
    {
        if (stat.LifeStart is not { } start)
            return;
        var life = now - start;
        if (life > stat.BestLife)
            stat.BestLife = life;
        stat.LifeStart = null;
    }

    // ═══════════════════════ Медали ═══════════════════════

    public void GrantMedals(EntityUid officer, int count)
    {
        if (count <= 0)
            return;
        var giver = EnsureComp<CadiaMedalGiverComponent>(officer);
        giver.Left = count;
        giver.Action = _actions.AddAction(officer, MedalAction);
    }

    private void OnAwardMedal(Entity<CadiaMedalGiverComponent> ent, ref CadiaAwardMedalEvent args)
    {
        if (args.Handled)
            return;
        var target = args.Target;
        if (!TryComp<CadiaFactionMemberComponent>(ent, out var mine) ||
            !TryComp<CadiaFactionMemberComponent>(target, out var theirs) ||
            theirs.Role == null || theirs.Faction != mine.Faction || !_mobState.IsAlive(target))
        {
            _popup.PopupEntity(Loc.GetString("cadia-medal-wrong-target"), ent, ent, PopupType.SmallCaution);
            return;
        }
        args.Handled = true;

        _hands.PickupOrDrop(target, Spawn(mine.Faction == CadiaFaction.Imperium ? MedalImperium : MedalChaos, Transform(target).Coordinates));
        var line = Loc.GetString("cadia-medal-award", ("officer", Name(ent)), ("soldier", Name(target)));
        _medals.Add(line);
        _rule.Announce(mine.Faction, line, MedalSound);
        _rule.Feed(line, mine.Faction);

        ent.Comp.Left--;
        if (ent.Comp.Left <= 0 && ent.Comp.Action is { } action)
        {
            _actions.RemoveAction(ent.Owner, action);
            RemCompDeferred<CadiaMedalGiverComponent>(ent);
        }
    }

    // ═══════════════════════ Итоги ═══════════════════════

    /// <summary>Строки для окна конца раунда. Номинация без отличившихся не показывается.</summary>
    public List<string> BuildReport()
    {
        var now = _timing.CurTime;
        var lines = new List<string>();

        // Кто дожил до конца, того жизнь ещё идёт.
        foreach (var stat in _players.Values)
        {
            if (stat.LifeStart is { } start && now - start > stat.BestLife)
                stat.BestLife = now - start;
        }

        void Add<T>(string key, Func<PlayerStat, T> value, Func<T, bool> worthy, Func<T, object> show) where T : IComparable<T>
        {
            if (_players.Count == 0)
                return;
            var best = _players.Values.MaxBy(value)!;
            var v = value(best);
            if (worthy(v))
                lines.Add(Loc.GetString(key, ("name", best.Name), ("value", show(v))));
        }

        Add("cadia-stats-kills", s => s.Kills, v => v > 0, v => v);
        Add("cadia-stats-pylons", s => s.Pylons, v => v > 0, v => v);
        Add("cadia-stats-healed", s => s.Healed, v => v > 0f, v => (int) v);
        Add("cadia-stats-life", s => s.BestLife, v => v > TimeSpan.Zero, v => (int) v.TotalMinutes);
        Add("cadia-stats-deaths", s => s.Deaths, v => v > 1, v => v);

        if (_vehicles.Count > 0)
        {
            var crew = _vehicles.Values.MaxBy(v => v.Kills)!;
            lines.Add(Loc.GetString("cadia-stats-crew", ("vehicle", crew.Name), ("value", crew.Kills), ("crew", string.Join(", ", crew.Crew))));
        }

        // Ход войны: что случилось за бой, без имён.
        foreach (var (what, count) in _counts.OrderBy(c => c.Key))
        {
            if (count > 0)
                lines.Add(Loc.GetString($"cadia-stats-war-{what.ToString().ToLowerInvariant()}", ("value", count)));
        }

        if (_medals.Count > 0)
        {
            lines.Add(Loc.GetString("cadia-stats-medals"));
            lines.AddRange(_medals);
        }
        return lines;
    }
}

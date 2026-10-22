using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Imperial.Cult.Components;
using Content.Server.Mind;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Cult;
using Content.Shared.Imperial.Cult.Components;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Components;
using Content.Shared.Pinpointer;
using Content.Shared.Roles.Jobs;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cult;

/// <summary>
/// Культ крови: datum/team/cult, datum/objective/sacrifice, datum/objective/eldergod, итоги раунда.
/// </summary>
public sealed class ImperialCultRuleSystem : GameRuleSystem<ImperialCultRuleComponent>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly PlayTimeTrackingManager _playTime = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly CultSystem _cult = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedJobSystem _job = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;

    public static readonly EntProtoId TeamRule = "CultTeam";
    private static readonly EntProtoId SacrificeObjective = "CultSacrificeTargetsObjective";
    private static readonly EntProtoId SummonObjective = "CultSummonNarSieObjective";

    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialCultRuleComponent, AfterAntagEntitySelectedEvent>(OnSelected);
        SubscribeLocalEvent<ImperialCultSacrificeConditionComponent, ObjectiveAfterAssignEvent>(OnSacrificeAssigned);
        SubscribeLocalEvent<ImperialCultSacrificeConditionComponent, ObjectiveGetProgressEvent>(OnSacrificeProgress);
        SubscribeLocalEvent<ImperialCultSummonConditionComponent, ObjectiveAfterAssignEvent>(OnSummonAssigned);
        SubscribeLocalEvent<ImperialCultSummonConditionComponent, ObjectiveGetProgressEvent>(OnSummonProgress);
    }

    #region Правило

    public bool TryGetRule([NotNullWhen(true)] out Entity<ImperialCultRuleComponent>? rule)
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out var uid, out _, out var comp, out _))
        {
            rule = (uid, comp);
            return true;
        }
        rule = null;
        return false;
    }

    /// <summary>Команда культа существует всегда, когда есть культист (даже выданный админом).</summary>
    public Entity<ImperialCultRuleComponent> EnsureRule()
    {
        if (TryGetRule(out var rule))
            return rule.Value;
        GameTicker.StartGameRule(TeamRule, out var ruleEnt);
        return (ruleEnt, Comp<ImperialCultRuleComponent>(ruleEnt));
    }

    /// <summary>
    /// restricted_roles: святых (капеллан) нельзя выбрать культистом. Компонент святости принадлежит
    /// фиче священника, поэтому он добавляется в чёрный список, только если зарегистрирован.
    /// </summary>
    protected override void Added(EntityUid uid, ImperialCultRuleComponent comp, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, comp, gameRule, args);

        if (!EntityManager.ComponentFactory.TryGetRegistration(CultForeignComponents.Holy, out _) ||
            !TryComp<AntagSelectionComponent>(uid, out var selection))
            return;

        // Чёрный список — копия из прототипа у сущности правила, кэш регистраций ещё пуст.
        foreach (var def in selection.Definitions)
        {
            if (def.Blacklist is not { } blacklist)
                continue;

            var components = blacklist.Components ?? Array.Empty<string>();
            if (!components.Contains(CultForeignComponents.Holy))
                blacklist.Components = components.Append(CultForeignComponents.Holy).ToArray();
        }
    }

    protected override void Started(EntityUid uid, ImperialCultRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);
        PickSummonSpots(comp);
    }

    private void OnSelected(Entity<ImperialCultRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        _cult.AddCultist(args.EntityUid, equip: true);
        ent.Comp.LeaderPending = _timing.CurTime + TimeSpan.FromSeconds(3);
    }

    public void AddMember(Entity<ImperialCultRuleComponent> rule, EntityUid mindId, bool cultGhost)
    {
        if (!rule.Comp.Members.Contains(mindId))
            rule.Comp.Members.Add(mindId);
        if (!cultGhost && !rule.Comp.TrueCultists.Contains(mindId))
            rule.Comp.TrueCultists.Add(mindId);
        rule.Comp.SizeAtMaximum = Math.Max(rule.Comp.SizeAtMaximum, rule.Comp.TrueCultists.Count);
        if (rule.Comp.SacrificeTarget == null && !rule.Comp.SacrificeDone)
            FindSacrificeTarget(rule);
    }

    public void RemoveMember(Entity<ImperialCultRuleComponent> rule, EntityUid mindId)
    {
        rule.Comp.Members.Remove(mindId);
        rule.Comp.TrueCultists.Remove(mindId);
    }

    public void EnsureObjectives(Entity<ImperialCultRuleComponent> rule, EntityUid mindId, MindComponent mind)
    {
        if (!mind.Objectives.Any(o => HasComp<ImperialCultSacrificeConditionComponent>(o)))
            _mind.TryAddObjective(mindId, mind, SacrificeObjective);
        if (!mind.Objectives.Any(o => HasComp<ImperialCultSummonConditionComponent>(o)))
            _mind.TryAddObjective(mindId, mind, SummonObjective);
    }

    /// <summary>datum/antagonist/cult/equip_cultist(): кинжал и 10 листов рунного металла.</summary>
    public void EquipCultist(EntityUid uid, Entity<ImperialCultRuleComponent> rule)
    {
        foreach (var proto in rule.Comp.StartingItems)
        {
            var item = Spawn(proto, Transform(uid).Coordinates);
            var where = Loc.GetString("cult-equip-floor");

            if (_inventory.TryGetSlotEntity(uid, "back", out var back) && TryComp<StorageComponent>(back, out var storage)
                && _storage.Insert(back.Value, item, out _, user: uid, storageComp: storage, playSound: false))
            {
                where = Loc.GetString("cult-equip-backpack");
            }
            else if (_inventory.TryEquip(uid, item, "pocket1", silent: true, force: true)
                     || _inventory.TryEquip(uid, item, "pocket2", silent: true, force: true))
            {
                where = Loc.GetString("cult-equip-pocket");
            }
            else if (_hands.TryPickupAnyHand(uid, item))
            {
                where = Loc.GetString("cult-equip-hands");
            }

            _cult.Message(uid, Loc.GetString("cult-equip-item", ("item", Name(item)), ("where", where)));
        }
        _cult.Message(uid, Loc.GetString("cult-equip-help"));
    }

    protected override void ActiveTick(EntityUid uid, ImperialCultRuleComponent comp, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, comp, gameRule, frameTime);
        var now = _timing.CurTime;

        if (comp.LeaderPending is { } pending && now >= pending)
        {
            comp.LeaderPending = null;
            if (_cult.GetLeader() == null)
                ChooseLeader();
        }

        if (now < _nextCheck)
            return;
        _nextCheck = now + TimeSpan.FromSeconds(2);

        // Цель жертвы исчезла, не будучи принесённой в жертву.
        if (!comp.SacrificeDone && comp.SacrificeTarget is { } target)
        {
            if (!TryComp<MindComponent>(target, out var mind) || mind.OwnedEntity is not { } body
                || TerminatingOrDeleted(body) || _cult.IsCultist(body))
            {
                comp.SacrificeTarget = null;
                FindSacrificeTarget((uid, comp));
                if (comp.SacrificeTarget != null)
                    _cult.MessageCult(Loc.GetString("cult-sacrifice-target-changed", ("target", SacrificeTargetName(comp))));
            }
        }
        else if (!comp.SacrificeDone && comp.SacrificeTarget == null)
        {
            FindSacrificeTarget((uid, comp));
        }
    }

    /// <summary>Мастером становится самый опытный культист (get_most_experienced).</summary>
    private void ChooseLeader()
    {
        EntityUid? best = null;
        var bestTime = TimeSpan.MinValue;
        foreach (var member in _cult.CultistBodies())
        {
            if (!TryComp<ActorComponent>(member, out var actor) || !HasComp<HumanoidProfileComponent>(member))
                continue;
            var time = _playTime.GetOverallPlaytime(actor.PlayerSession);
            if (time <= bestTime)
                continue;
            bestTime = time;
            best = member;
        }

        if (best != null)
            _cult.MakeLeader(best.Value);
    }

    #endregion

    #region Жертва

    public bool IsSacrificeTarget(EntityUid mindId)
    {
        return TryGetRule(out var rule) && rule.Value.Comp.SacrificeTarget == mindId && !rule.Value.Comp.SacrificeDone;
    }

    /// <summary>datum/objective/sacrifice/find_sac_target().</summary>
    public void FindSacrificeTarget(Entity<ImperialCultRuleComponent> rule)
    {
        var unconvertable = new List<EntityUid>();
        var any = new List<EntityUid>();
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } mob || !HasComp<HumanoidProfileComponent>(mob) || _mobState.IsDead(mob))
                continue;
            if (_cult.IsCultist(mob) || !_mind.TryGetMind(mob, out var mindId, out _))
                continue;
            if (_station.GetOwningStation(mob) == null)
                continue;
            any.Add(mindId);
            if (!_cult.IsConvertable(mob))
                unconvertable.Add(mindId);
        }

        var pool = unconvertable.Count > 0 ? unconvertable : any;
        if (pool.Count == 0)
        {
            // Почти все — культисты: цель считается выполненной, чтобы не было тупика.
            rule.Comp.SacrificeDone = true;
            UpdateObjectiveTexts(rule.Comp);
            return;
        }

        rule.Comp.SacrificeTarget = _random.Pick(pool);
        UpdateObjectiveTexts(rule.Comp);
    }

    /// <summary>Жертва цели принесена (руна Подношения).</summary>
    public void SacrificeCompleted(EntityUid victimMind)
    {
        if (!TryGetRule(out var rule) || rule.Value.Comp.SacrificeTarget != victimMind)
            return;
        rule.Value.Comp.SacrificeDone = true;
        UpdateObjectiveTexts(rule.Value.Comp);
    }

    public string SacrificeTargetName(ImperialCultRuleComponent comp)
    {
        if (comp.SacrificeTarget is not { } target || !TryComp<MindComponent>(target, out var mind))
            return Loc.GetString("cult-objective-unknown");
        var name = mind.CharacterName ?? Loc.GetString("cult-objective-unknown");
        return Loc.GetString("cult-objective-target-format", ("name", name), ("job", _job.MindTryGetJobName(target)));
    }

    #endregion

    #region Места призыва

    /// <summary>datum/objective/eldergod: SUMMON_POSSIBILITIES случайных отсеков станции (по маякам навкарты).</summary>
    private void PickSummonSpots(ImperialCultRuleComponent comp)
    {
        comp.SummonSpots.Clear();
        comp.SummonSpotNames.Clear();

        var stations = _station.GetStations();
        var grids = stations.Select(s => _station.GetLargestGrid(s)).Where(g => g != null).Select(g => g!.Value).ToHashSet();

        var candidates = new List<(EntityUid, string)>();
        var query = EntityQueryEnumerator<ConfigurableNavMapBeaconComponent, NavMapBeaconComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var beacon, out var xform))
        {
            if (!beacon.Enabled || string.IsNullOrWhiteSpace(beacon.Text))
                continue;
            if (xform.GridUid is not { } grid || grids.Count > 0 && !grids.Contains(grid))
                continue;
            if (candidates.Any(c => c.Item2 == beacon.Text))
                continue;
            candidates.Add((uid, beacon.Text!));
        }

        _random.Shuffle(candidates);
        foreach (var (uid, name) in candidates.Take(comp.SummonSpotCount))
        {
            comp.SummonSpots.Add(uid);
            comp.SummonSpotNames.Add(name);
        }

        UpdateObjectiveTexts(comp);
    }

    /// <summary>Ближайший маяк навкарты — это «отсек», в котором стоит сущность.</summary>
    public EntityUid? GetAreaBeacon(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.GridUid == null)
            return null;
        var pos = _transform.GetWorldPosition(xform);

        EntityUid? best = null;
        var bestDist = float.MaxValue;
        var query = EntityQueryEnumerator<ConfigurableNavMapBeaconComponent, NavMapBeaconComponent, TransformComponent>();
        while (query.MoveNext(out var beaconUid, out _, out var beacon, out var bxform))
        {
            if (!beacon.Enabled || beacon.Text == null || bxform.GridUid != xform.GridUid)
                continue;
            var dist = (_transform.GetWorldPosition(bxform) - pos).LengthSquared();
            if (dist >= bestDist)
                continue;
            bestDist = dist;
            best = beaconUid;
        }
        return best;
    }

    public bool IsInSummonSpot(EntityUid uid)
    {
        if (!TryGetRule(out var rule))
            return false;
        var beacon = GetAreaBeacon(uid);
        if (beacon == null)
            return false;
        // Маяки с тем же названием — один отсек.
        var text = CompOrNull<NavMapBeaconComponent>(beacon)?.Text;
        return rule.Value.Comp.SummonSpots.Contains(beacon.Value) || text != null && rule.Value.Comp.SummonSpotNames.Contains(text);
    }

    /// <summary>Руна апокалипсиса убирает место призыва, если их больше одного.</summary>
    public bool RemoveSummonSpot(EntityUid uid)
    {
        if (!TryGetRule(out var rule) || rule.Value.Comp.SummonSpots.Count <= 1)
            return false;
        var beacon = GetAreaBeacon(uid);
        var text = beacon == null ? null : CompOrNull<NavMapBeaconComponent>(beacon)?.Text;
        var idx = text == null ? -1 : rule.Value.Comp.SummonSpotNames.IndexOf(text);
        if (idx < 0)
            return false;
        rule.Value.Comp.SummonSpots.RemoveAt(idx);
        rule.Value.Comp.SummonSpotNames.RemoveAt(idx);
        UpdateObjectiveTexts(rule.Value.Comp);
        return true;
    }

    public string SummonSpotsText(ImperialCultRuleComponent comp)
    {
        if (comp.SummonSpotNames.Count == 0)
            return Loc.GetString("cult-objective-anywhere");
        if (comp.SummonSpotNames.Count == 1)
            return comp.SummonSpotNames[0];
        return string.Join(", ", comp.SummonSpotNames.Take(comp.SummonSpotNames.Count - 1))
               + " " + Loc.GetString("cult-objective-and") + " " + comp.SummonSpotNames[^1];
    }

    #endregion

    #region Цели

    private void OnSacrificeAssigned(Entity<ImperialCultSacrificeConditionComponent> ent, ref ObjectiveAfterAssignEvent args)
    {
        if (TryGetRule(out var rule))
            UpdateSacrificeText(ent, rule.Value.Comp);
    }

    private void OnSacrificeProgress(Entity<ImperialCultSacrificeConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = TryGetRule(out var rule) && rule.Value.Comp.SacrificeDone ? 1f : 0f;
    }

    private void OnSummonAssigned(Entity<ImperialCultSummonConditionComponent> ent, ref ObjectiveAfterAssignEvent args)
    {
        if (TryGetRule(out var rule))
            UpdateSummonText(ent, rule.Value.Comp);
    }

    private void OnSummonProgress(Entity<ImperialCultSummonConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = TryGetRule(out var rule) && rule.Value.Comp.NarSieSummoned ? 1f : 0f;
    }

    public void UpdateObjectiveTexts(ImperialCultRuleComponent comp)
    {
        var sac = EntityQueryEnumerator<ImperialCultSacrificeConditionComponent>();
        while (sac.MoveNext(out var uid, out _))
            UpdateSacrificeText(uid, comp);
        var sum = EntityQueryEnumerator<ImperialCultSummonConditionComponent>();
        while (sum.MoveNext(out var uid, out _))
            UpdateSummonText(uid, comp);
    }

    private void UpdateSacrificeText(EntityUid objective, ImperialCultRuleComponent comp)
    {
        var text = comp.SacrificeTarget == null
            ? Loc.GetString("cult-objective-sacrifice-free")
            : Loc.GetString("cult-objective-sacrifice", ("target", SacrificeTargetName(comp)));
        _meta.SetEntityName(objective, text);
    }

    private void UpdateSummonText(EntityUid objective, ImperialCultRuleComponent comp)
    {
        _meta.SetEntityName(objective, Loc.GetString("cult-objective-summon", ("spots", SummonSpotsText(comp))));
    }

    #endregion

    /// <summary>get_random_station_turf().</summary>
    public bool TryRandomStationTile(out EntityCoordinates coords)
    {
        coords = default;
        if (!TryFindRandomTile(out _, out _, out _, out var found))
            return false;
        coords = found;
        return true;
    }

    #region Итоги

    protected override void AppendRoundEndText(EntityUid uid, ImperialCultRuleComponent comp, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, comp, gameRule, ref args);

        if (comp.NarSieKilled)
            args.AddLine(Loc.GetString("cult-roundend-narsie-killed"));
        else if (comp.NarSieSummoned)
            args.AddLine(Loc.GetString("cult-roundend-win"));
        else
            args.AddLine(Loc.GetString("cult-roundend-loss"));

        args.AddLine(Loc.GetString(comp.SacrificeDone ? "cult-roundend-sacrifice-done" : "cult-roundend-sacrifice-failed",
            ("target", SacrificeTargetName(comp))));
        args.AddLine(Loc.GetString(comp.NarSieSummoned ? "cult-roundend-summon-done" : "cult-roundend-summon-failed"));
        args.AddLine(Loc.GetString("cult-roundend-size", ("count", comp.SizeAtMaximum)));
        args.AddLine(Loc.GetString("cult-roundend-cultists"));

        foreach (var mindId in comp.Members)
        {
            if (!TryComp<MindComponent>(mindId, out var mind))
                continue;
            var name = mind.CharacterName ?? "?";
            var user = mind.OriginalOwnerUserId is { } userId && _players.TryGetPlayerData(userId, out var data) ? data.UserName : "?";
            var leader = mindId == comp.LeaderMind;
            args.AddLine(Loc.GetString(leader ? "cult-roundend-master-entry" : "cult-roundend-cultist-entry", ("name", name), ("user", user)));
        }
        args.AddLine("");
    }

    #endregion
}

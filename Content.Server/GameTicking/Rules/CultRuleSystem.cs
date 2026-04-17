using System.Diagnostics.CodeAnalysis;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Imperial.Cult;
using Content.Server.Mind;
using Content.Server.Pinpointer;
using Content.Server.RoundEnd;
using Content.Server.Station.Systems;
using Content.Shared.Body.Events;
using Content.Shared.GameTicking.Components;
using Content.Shared.Gibbing;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Cult.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Pinpointer;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.GameTicking.Rules;

/// <summary>
/// Управляет раундом культа Нар'Си: выбирает обязательные жертвы, отслеживает прогресс ритуала и завершает режим при успешном призыве.
/// </summary>
public sealed class CultRuleSystem : GameRuleSystem<CultRuleComponent>
{
    private const int NarSieBeaconCount = 3;

    private static readonly HashSet<string> PreferredSacrificeJobs = new()
    {
        "Captain",
        "HeadOfPersonnel",
        "HeadOfSecurity",
        "ChiefEngineer",
        "ChiefMedicalOfficer",
        "ResearchDirector",
        "Quartermaster",
        "Warden",
        "SecurityOfficer",
        "Detective",
        "SecurityCadet",
    };

    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly CultRuneSystem _cultRune = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedJobSystem _job = default!;
    [Dependency] private readonly StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CultSacrificeCompletedEvent>(OnSacrifice);
        SubscribeLocalEvent<CultNarSieSummonedEvent>(OnNarSieSummoned);
        SubscribeLocalEvent<CultRuleComponent, AfterAntagEntitySelectedEvent>(OnCultistSelected);
        SubscribeLocalEvent<MindContainerComponent, MobStateChangedEvent>(OnTargetMobStateChanged);
        SubscribeLocalEvent<MindContainerComponent, ComponentShutdown>(OnTargetMindShutdown);
    }

    private void OnCultistSelected(Entity<CultRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        EnsureSacrificeTargets(ent);
        EnsureNarSieBeacons(ent, _station.GetOwningStation(args.EntityUid));

        if (!_mind.TryGetMind(args.EntityUid, out var mindId, out var mind))
        {
            Log.Error($"[Cult] OnCultistSelected: no mind found for {ToPrettyString(args.EntityUid)}");
            return;
        }

        _mind.TryAddObjective(mindId, mind, "CultSacrificeTargetsObjective");
        _mind.TryAddObjective(mindId, mind, "CultSummonNarSieObjective");
    }

    protected override void Started(EntityUid uid, CultRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);
        EnsureSacrificeTargets(uid, comp);
    }

    protected override void AppendRoundEndText(EntityUid uid, CultRuleComponent comp, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, comp, gameRule, ref args);

        SyncCompletedSacrificeTargets(uid, comp);

        if (comp.NarSieSummoned)
        {
            args.AddLine(Loc.GetString("cult-roundend-win"));
            return;
        }

        args.AddLine(Loc.GetString("cult-roundend-lose",
            ("sacrifices", comp.CompletedSacrificeTargets.Count),
            ("required", GetEffectiveSacrificeRequirement(comp))));

        args.AddLine(Loc.GetString("cult-roundend-cultists"));

        var antags = _antag.GetAntagIdentifiers(uid);
        foreach (var (_, sessionData, name) in antags)
        {
            args.AddLine(Loc.GetString("cult-roundend-cultist-entry",
                ("name", name),
                ("user", sessionData.UserName)));
        }

        args.AddLine("");
    }

    public bool TryGetActiveCultRule([NotNullWhen(true)] out EntityUid? uid, [NotNullWhen(true)] out CultRuleComponent? comp)
    {
        var query = EntityQueryEnumerator<CultRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var ruleUid, out var cultRule, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(ruleUid, gameRule))
                continue;

            uid = ruleUid;
            comp = cultRule;
            return true;
        }

        uid = null;
        comp = null;
        return false;
    }

    public IReadOnlyList<EntityUid> GetSacrificeTargets()
    {
        if (!TryGetActiveCultRule(out var uid, out var comp))
            return Array.Empty<EntityUid>();

        EnsureSacrificeTargets(uid, comp);
        return comp.SacrificeTargets;
    }

    public float GetSacrificeProgress()
    {
        if (!TryGetActiveCultRule(out var uid, out var comp))
            return 0f;

        EnsureSacrificeTargets(uid, comp);
        SyncCompletedSacrificeTargets(uid.Value, comp);
        var required = GetEffectiveSacrificeRequirement(comp);
        return required <= 0 ? 1f : Math.Min((float) comp.CompletedSacrificeTargets.Count / required, 1f);
    }

    public bool IsNarSieSummoned()
    {
        return TryGetActiveCultRule(out _, out var comp) && comp.NarSieSummoned;
    }

    public IReadOnlyList<string> GetNarSieBeaconLabels(EntityUid? stationUid = null)
    {
        if (!TryGetActiveCultRule(out var uid, out var comp))
            return Array.Empty<string>();

        EnsureNarSieBeacons(uid, comp, stationUid);
        return comp.NarSieBeaconLabels;
    }

    public string GetNarSieBeaconSummary(EntityUid? stationUid = null)
    {
        var labels = GetNarSieBeaconLabels(stationUid);
        return labels.Count == 0
            ? Loc.GetString("cult-narsie-unknown-location")
            : string.Join(", ", labels);
    }

    public bool IsNearNarSieBeacon(MapCoordinates coordinates, EntityUid? stationUid, float range)
    {
        if (!TryGetActiveCultRule(out var uid, out var comp))
            return false;

        EnsureNarSieBeacons(uid, comp, stationUid);

        foreach (var beaconUid in comp.NarSieBeaconTargets)
        {
            if (!TryComp<NavMapBeaconComponent>(beaconUid, out var beacon))
                continue;

            if (!Exists(beaconUid))
                continue;

            var xform = Transform(beaconUid);

            if (!_navMap.TryGetBeaconLabel(beaconUid, out _, beacon))
                continue;

            if (stationUid != null && _station.GetOwningStation(beaconUid) != stationUid)
                continue;

            if (xform.MapID != coordinates.MapId)
                continue;

            if ((xform.WorldPosition - coordinates.Position).Length() <= range)
                return true;
        }

        return false;
    }

    public bool TryGetNearestNarSieBeaconLabel(MapCoordinates coordinates, EntityUid? stationUid, [NotNullWhen(true)] out string? label)
    {
        label = null;

        if (!TryGetActiveCultRule(out var uid, out var comp))
            return false;

        EnsureNarSieBeacons(uid, comp, stationUid);

        var nearestDistance = float.PositiveInfinity;
        foreach (var beaconUid in comp.NarSieBeaconTargets)
        {
            if (!TryComp<NavMapBeaconComponent>(beaconUid, out var beacon))
                continue;

            if (!Exists(beaconUid))
                continue;

            var xform = Transform(beaconUid);

            if (!_navMap.TryGetBeaconLabel(beaconUid, out var candidate, beacon))
                continue;

            if (stationUid != null && _station.GetOwningStation(beaconUid) != stationUid)
                continue;

            if (xform.MapID != coordinates.MapId)
                continue;

            var distance = (xform.WorldPosition - coordinates.Position).LengthSquared();
            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            label = candidate;
        }

        return label != null;
    }

    public bool AreRequiredSacrificesComplete(out int completed, out int required)
    {
        completed = 0;
        required = 0;

        if (!TryGetActiveCultRule(out var uid, out var comp))
            return false;

        EnsureSacrificeTargets(uid, comp);
        SyncCompletedSacrificeTargets(uid.Value, comp);
        completed = comp.CompletedSacrificeTargets.Count;
        required = GetEffectiveSacrificeRequirement(comp);
        return completed >= required;
    }

    public bool IsSacrificeTarget(EntityUid victim)
    {
        if (!TryGetActiveCultRule(out var uid, out var comp))
            return false;

        EnsureSacrificeTargets(uid, comp);

        if (!_mind.TryGetMind(victim, out var victimMindId, out _))
            return false;

        return comp.SacrificeTargets.Contains(victimMindId);
    }

    public bool TryAnnounceVeilBroken()
    {
        if (!TryGetActiveCultRule(out var uid, out var comp))
            return false;

        if (comp.VeilBrokenAnnounced)
            return false;

        comp.VeilBrokenAnnounced = true;
        Dirty(uid.Value, comp);
        return true;
    }

    public bool TryAnnounceVeilWeakens()
    {
        if (!TryGetActiveCultRule(out var uid, out var comp))
            return false;

        if (comp.VeilWeakensAnnounced)
            return false;

        comp.VeilWeakensAnnounced = true;
        Dirty(uid.Value, comp);
        return true;
    }

    private void OnSacrifice(CultSacrificeCompletedEvent ev)
    {
        TryCompleteSacrificeTarget(ev.Victim, ev.VictimMind, broadcastWrongTarget: true);
    }

    private void OnTargetMobStateChanged(EntityUid uid, MindContainerComponent comp, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        TryCompleteSacrificeTarget(uid);
    }

    private void OnTargetMindShutdown(EntityUid uid, MindContainerComponent comp, ref ComponentShutdown args)
    {
        TryCompleteSacrificeTarget(uid);
    }

    private void OnNarSieSummoned(CultNarSieSummonedEvent ev)
    {
        var query = EntityQueryEnumerator<CultRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var comp, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            EnsureSacrificeTargets(uid, comp);
            SyncCompletedSacrificeTargets(uid, comp);
            var required = GetEffectiveSacrificeRequirement(comp);
            if (comp.CompletedSacrificeTargets.Count < required)
            {
                _cultRune.BroadcastCultMessage(Loc.GetString("cult-commune-sacrifices-required-first"));
                return;
            }

            comp.NarSieSummoned = true;
            Dirty(uid, comp);

            _audio.PlayGlobal(
                "/Audio/Imperial/cult/ambience/cult_win.ogg",
                Filter.Broadcast(),
                true,
                AudioParams.Default.WithVolume(5f));

            if (_roundEnd.IsRoundEndRequested())
                _roundEnd.CancelRoundEndCountdown(forceRecall: true);

            _roundEnd.EndRound();
        }
    }

    private void EnsureNarSieBeacons(EntityUid? uid, CultRuleComponent comp, EntityUid? stationUid = null)
    {
        if (uid == null)
            return;

        EnsureNarSieBeacons((uid.Value, comp), stationUid);
    }

    private void EnsureNarSieBeacons(Entity<CultRuleComponent> ent, EntityUid? stationUid = null)
    {
        if (ent.Comp.NarSieBeaconTargets.Count > 0 && ent.Comp.NarSieBeaconLabels.Count > 0)
        {
            if (stationUid == null)
                return;

            var allMatchStation = true;
            foreach (var target in ent.Comp.NarSieBeaconTargets)
            {
                if (!Exists(target)
                    || TerminatingOrDeleted(target)
                    || _station.GetOwningStation(target) != stationUid)
                {
                    allMatchStation = false;
                    break;
                }
            }

            if (allMatchStation)
                return;
        }

        var candidates = new List<(EntityUid Uid, string Label)>();
        var query = EntityQueryEnumerator<NavMapBeaconComponent>();
        while (query.MoveNext(out var beaconUid, out var beacon))
        {
            if (!_navMap.TryGetBeaconLabel(beaconUid, out var label, beacon))
                continue;

            if (stationUid != null && _station.GetOwningStation(beaconUid) != stationUid)
                continue;

            candidates.Add((beaconUid, label));
        }

        if (candidates.Count == 0 && stationUid != null)
        {
            EnsureNarSieBeacons(ent, null);
            return;
        }

        ent.Comp.NarSieBeaconTargets.Clear();
        ent.Comp.NarSieBeaconLabels.Clear();

        while (candidates.Count > 0 && ent.Comp.NarSieBeaconTargets.Count < NarSieBeaconCount)
        {
            var index = _random.Next(candidates.Count);
            var candidate = candidates[index];
            candidates.RemoveAt(index);

            ent.Comp.NarSieBeaconTargets.Add(candidate.Uid);
            ent.Comp.NarSieBeaconLabels.Add(candidate.Label);
        }

        Dirty(ent);
    }

    private void EnsureSacrificeTargets(EntityUid? uid, CultRuleComponent comp)
    {
        if (uid == null)
            return;

        EnsureSacrificeTargets((uid.Value, comp));
    }

    private void EnsureSacrificeTargets(Entity<CultRuleComponent> ent)
    {
        if (ent.Comp.TargetsInitialized && ent.Comp.SacrificeTargets.Count >= GetEffectiveSacrificeRequirement(ent.Comp))
            return;

        var generalCandidates = new List<EntityUid>();
        var priorityCandidates = new List<EntityUid>();
        var query = EntityQueryEnumerator<MindComponent>();

        while (query.MoveNext(out var mindUid, out var mind))
        {
            if (mind.OwnedEntity is not { } owned)
                continue;

            if (!Exists(owned) || TerminatingOrDeleted(owned))
                continue;

            if (_mind.IsCharacterDeadIc(mind))
                continue;

            if (HasComp<CultistComponent>(owned))
                continue;

            if (!_job.MindTryGetJobId(mindUid, out var jobId) || jobId == null)
                continue;

            generalCandidates.Add(mindUid);

            if (PreferredSacrificeJobs.Contains(jobId.Value))
                priorityCandidates.Add(mindUid);
        }

        var selectedTargets = new List<EntityUid>();
        SelectTargets(priorityCandidates, selectedTargets, ent.Comp.RequiredSacrifices, generalCandidates);
        SelectTargets(generalCandidates, selectedTargets, ent.Comp.RequiredSacrifices, null);

        ent.Comp.SacrificeTargets.Clear();
        ent.Comp.CompletedSacrificeTargets.Clear();
        ent.Comp.SacrificeTargets.AddRange(selectedTargets);
        ent.Comp.TargetsInitialized = true;
        Dirty(ent);
    }

    private static int GetEffectiveSacrificeRequirement(CultRuleComponent comp)
    {
        if (comp.SacrificeTargets.Count == 0)
            return 0;

        return Math.Min(comp.RequiredSacrifices, comp.SacrificeTargets.Count);
    }

    private void SyncCompletedSacrificeTargets(EntityUid uid, CultRuleComponent comp)
    {
        var changed = false;

        foreach (var targetMindUid in comp.SacrificeTargets)
        {
            if (comp.CompletedSacrificeTargets.Contains(targetMindUid))
                continue;

            if (!TryComp<MindComponent>(targetMindUid, out var targetMind))
                continue;

            if (!IsSacrificeTargetCompleted(targetMind))
                continue;

            comp.CompletedSacrificeTargets.Add(targetMindUid);
            changed = true;
        }

        if (changed)
            Dirty(uid, comp);
    }

    private bool IsSacrificeTargetCompleted(MindComponent targetMind)
    {
        if (targetMind.TimeOfDeath != null)
            return true;

        if (targetMind.OwnedEntity is not { } owned)
            return true;

        if (HasComp<GhostComponent>(owned))
            return true;

        return _mind.IsCharacterDeadIc(targetMind);
    }

    private bool TryCompleteSacrificeTarget(EntityUid victim, EntityUid? victimMindId = null, bool broadcastWrongTarget = false)
    {
        if (!TryGetActiveCultRule(out var uid, out var comp))
            return false;

        EnsureSacrificeTargets(uid, comp);
        SyncCompletedSacrificeTargets(uid.Value, comp);

        if (victimMindId == null)
        {
            if (!_mind.TryGetMind(victim, out var resolvedMindId, out _))
                victimMindId = FindFallbackSacrificeTargetMind(victim, comp);
            else
                victimMindId = resolvedMindId;
        }

        if (victimMindId != null && !comp.SacrificeTargets.Contains(victimMindId.Value))
            victimMindId = FindFallbackSacrificeTargetMind(victim, comp);

        if (victimMindId == null)
            return false;

        if (!comp.SacrificeTargets.Contains(victimMindId.Value))
        {
            if (broadcastWrongTarget)
                _cultRune.BroadcastCultMessage(Loc.GetString("cult-commune-sacrifice-wrong-target"));

            return false;
        }

        if (comp.CompletedSacrificeTargets.Contains(victimMindId.Value))
            return false;

        comp.CompletedSacrificeTargets.Add(victimMindId.Value);
        Dirty(uid.Value, comp);

        var required = GetEffectiveSacrificeRequirement(comp);
        _cultRune.BroadcastCultMessage(
            Loc.GetString("cult-commune-sacrifices-count",
                ("done", comp.CompletedSacrificeTargets.Count),
                ("needed", required)));

        if (comp.CompletedSacrificeTargets.Count >= required)
            _cultRune.BroadcastCultMessage(Loc.GetString("cult-commune-can-summon"));

        return true;
    }

    private EntityUid? FindFallbackSacrificeTargetMind(EntityUid victim, CultRuleComponent comp)
    {
        var victimNet = GetNetEntity(victim);
        var victimName = MetaData(victim).EntityName;

        foreach (var targetMindUid in comp.SacrificeTargets)
        {
            if (!TryComp<MindComponent>(targetMindUid, out var targetMind))
                continue;

            if (targetMind.OriginalOwnedEntity == victimNet)
                return targetMindUid;

            if (!string.IsNullOrWhiteSpace(targetMind.CharacterName) && targetMind.CharacterName == victimName)
                return targetMindUid;
        }

        return null;
    }

    private void SelectTargets(List<EntityUid> pool, List<EntityUid> selectedTargets, int required, List<EntityUid>? secondaryPool)
    {
        while (pool.Count > 0 && selectedTargets.Count < required)
        {
            var index = _random.Next(pool.Count);
            var target = pool[index];
            pool.RemoveAt(index);
            secondaryPool?.Remove(target);
            selectedTargets.Add(target);
        }
    }
}


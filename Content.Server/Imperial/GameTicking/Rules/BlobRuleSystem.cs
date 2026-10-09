using Content.Server.Antag;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Imperial.Blob;
using Content.Server.Imperial.Blob.Components;
using Content.Server.Mind;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Imperial.Blob.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Server.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking.Rules;

/// <summary>
/// Правило блоба: носитель (blob infection) или оверманд из призраков (midround blob).
/// </summary>
public sealed class BlobRuleSystem : StationEventSystem<BlobRuleComponent>
{
    private static readonly Color BlobBriefingColor = Color.FromHex("#5acb74");
    private static readonly EntProtoId BlobObjective = "ImperialBlobTakeoverObjective";

    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly BlobOvermindSystem _overmind = default!;

    /// <summary>Итоги для roundend_report: имя оверманда и наибольший размер.</summary>
    private readonly Dictionary<EntityUid, (string Name, int MaxCount, bool Victory)> _results = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlobRuleComponent, AfterAntagEntitySelectedEvent>(OnAfterAntagSelected);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _results.Clear());
    }

    protected override void Started(EntityUid uid, BlobRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!component.SpawnGhostRoleAtVentOnStart)
            return;

        // midround/from_ghosts/blob: оверманд появляется в случайной точке blobstart.
        if (_overmind.FindRandomBlobStart() is { } start)
            Spawn(component.GhostRoleSpawnerPrototype, start);
    }

    private void OnAfterAntagSelected(Entity<BlobRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        if (!_mind.TryGetMind(args.EntityUid, out var mindId, out var mind))
            return;

        EnsureBlobObjective(mindId, mind);
        if (!ent.Comp.BlobMinds.Contains(mindId))
            ent.Comp.BlobMinds.Add(mindId);

        var carrier = EnsureComp<BlobCarrierComponent>(args.EntityUid);
        carrier.Rule = ent.Owner;

        if (mind.UserId != null && _players.TryGetSessionById(mind.UserId.Value, out var session))
            _antag.SendBriefing(session, Loc.GetString("blob-role-greeting-carrier"), BlobBriefingColor, ent.Comp.GreetSoundNotification);
    }

    /// <summary>mind_initialize(): у оверманда всегда есть цель блоба.</summary>
    public void EnsureBlobAntag(EntityUid mindId, BlobOvermindComponent overmind)
    {
        if (!TryComp<MindComponent>(mindId, out var mind))
            return;

        EnsureBlobObjective(mindId, mind);

        var rules = EntityQueryEnumerator<BlobRuleComponent>();
        while (rules.MoveNext(out _, out var rule))
        {
            if (!rule.BlobMinds.Contains(mindId))
                rule.BlobMinds.Add(mindId);
            break;
        }
    }

    private void EnsureBlobObjective(EntityUid mindId, MindComponent mind)
    {
        if (_mind.TryFindObjective((mindId, mind), BlobObjective, out _))
            return;

        if (!_mind.TryAddObjective(mindId, mind, BlobObjective))
            Log.Warning($"Failed to add {BlobObjective} to {ToPrettyString(mindId)}");
    }

    public void RecordResult(Entity<BlobOvermindComponent> ent)
    {
        if (!TryComp<MindContainerComponent>(ent, out var container) || container.Mind is not { } mindId)
            return;
        _results[mindId] = (Name(ent), Math.Max(ent.Comp.MaxCount, ent.Comp.BlobsLegit.Count), ent.Comp.Victorious);
    }

    /// <summary>Прогресс цели: blobs_legit.len / blobwincount.</summary>
    public int GetOwnedStationTileCount(EntityUid mindId)
    {
        var query = EntityQueryEnumerator<BlobOvermindComponent, MindContainerComponent>();
        while (query.MoveNext(out _, out var overmind, out var container))
        {
            if (container.Mind != mindId)
                continue;
            return overmind.Victorious ? int.MaxValue : overmind.BlobsLegit.Count;
        }

        return _results.TryGetValue(mindId, out var result) && result.Victory ? int.MaxValue : 0;
    }

    protected override void AppendRoundEndText(EntityUid uid, BlobRuleComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        var query = EntityQueryEnumerator<BlobOvermindComponent, MindContainerComponent>();
        while (query.MoveNext(out var overmindUid, out var overmind, out var container))
        {
            if (container.Mind is { } mindId)
                _results[mindId] = (Name(overmindUid), Math.Max(overmind.MaxCount, overmind.BlobsLegit.Count), overmind.Victorious);
        }

        foreach (var mindId in component.BlobMinds)
        {
            if (!_results.TryGetValue(mindId, out var result))
                continue;

            args.AddLine(result.Victory
                ? Loc.GetString("blob-round-end-overmind-won", ("name", result.Name))
                : Loc.GetString("blob-round-end-overmind-lost", ("name", result.Name), ("count", result.MaxCount)));
        }
    }
}

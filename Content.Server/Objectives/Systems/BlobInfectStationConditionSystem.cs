using System;
using Content.Server.GameTicking.Rules;
using Content.Server.Objectives.Components;
using Content.Shared.Objectives.Components;

namespace Content.Server.Objectives.Systems;

public sealed class BlobInfectStationConditionSystem : EntitySystem
{
    [Dependency] private readonly BlobRuleSystem _blobRule = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobInfectStationConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, BlobInfectStationConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        if (!TryComp<NumberObjectiveComponent>(uid, out var number) || number.Target <= 0)
        {
            args.Progress = 0f;
            return;
        }

        var infectedTiles = _blobRule.GetOwnedStationTileCount(args.MindId);
        args.Progress = Math.Clamp((float) infectedTiles / number.Target, 0f, 1f);
    }
}
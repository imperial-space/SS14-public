using Content.Server.Antag;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.GameTicking;
using Content.Server.Objectives;
using Content.Server.Objectives.Systems;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server.GameTicking.Rules;

public sealed class BloodBrotherRuleSystem : GameRuleSystem<BloodBrotherRuleComponent>
{
    private static readonly Color BloodBrotherBriefingColor = Color.FromHex("#8b1e1e");
    private const string DefaultBloodBrotherRule = "BloodBrother";

    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly TargetObjectiveSystem _targetObjective = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodBrotherRuleComponent, AfterAntagEntitySelectedEvent>(OnAfterAntagSelected);
    }

    public bool TryMakeBloodBrotherPair(ICommonSession first, ICommonSession second)
    {
        if (first == second)
            return false;

        var rule = _antag.ForceGetGameRuleEnt<BloodBrotherRuleComponent>(DefaultBloodBrotherRule);
        if (!_antag.TryGetNextAvailableDefinition(rule, out var definition))
            definition = rule.Comp.Definitions[^1];

        _antag.MakeAntag(rule, first, definition.Value);
        _antag.MakeAntag(rule, second, definition.Value);
        return true;
    }

    public bool TryMakeSoloBloodBrother(ICommonSession player)
    {
        var rule = _antag.ForceGetGameRuleEnt<BloodBrotherRuleComponent>(DefaultBloodBrotherRule);
        if (!_antag.TryGetNextAvailableDefinition(rule, out var definition))
            definition = rule.Comp.Definitions[^1];

        _antag.MakeAntag(rule, player, definition.Value);
        return true;
    }

    private void OnAfterAntagSelected(Entity<BloodBrotherRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        if (!_mind.TryGetMind(args.EntityUid, out var mindId, out var mind))
            return;

        if (!ent.Comp.BloodBrotherMinds.Contains(mindId))
            ent.Comp.BloodBrotherMinds.Add(mindId);

        if (ent.Comp.ObjectivesAssigned)
            return;

        if (ent.Comp.BloodBrotherMinds.Count >= 2)
        {
            if (!TryComp(ent.Comp.BloodBrotherMinds[0], out MindComponent? firstMind) ||
                !TryComp(ent.Comp.BloodBrotherMinds[1], out MindComponent? secondMind))
            {
                return;
            }

            AssignSharedObjectives((ent.Comp.BloodBrotherMinds[0], firstMind), (ent.Comp.BloodBrotherMinds[1], secondMind), ent.Comp);
            ent.Comp.ObjectivesAssigned = true;

            SendBloodBrotherBriefing((ent.Comp.BloodBrotherMinds[0], firstMind), (ent.Comp.BloodBrotherMinds[1], secondMind), ent.Comp);
            SendBloodBrotherBriefing((ent.Comp.BloodBrotherMinds[1], secondMind), (ent.Comp.BloodBrotherMinds[0], firstMind), ent.Comp);
            return;
        }

        if (ent.Comp.BloodBrotherMinds.Count != 1 || !ShouldAssignSoloObjectives(mind))
            return;

        AssignSoloObjectives((mindId, mind), ent.Comp);
        ent.Comp.ObjectivesAssigned = true;
        SendSoloBloodBrotherBriefing((mindId, mind), ent.Comp);
    }

    private bool ShouldAssignSoloObjectives(MindComponent mind)
    {
        if (mind.UserId == null || !_players.TryGetSessionById(mind.UserId.Value, out var session))
            return false;

        foreach (var other in _players.Sessions)
        {
            if (other == session || other.AttachedEntity == null)
                continue;

            if (HasComp<MindContainerComponent>(other.AttachedEntity.Value))
                return false;
        }

        return true;
    }

    private void AssignSharedObjectives(Entity<MindComponent> first, Entity<MindComponent> second, BloodBrotherRuleComponent comp)
    {
        AddPartnerObjective(first, second.Owner);
        AddPartnerObjective(second, first.Owner);

        if (_objectives.TryCreateObjective(first, comp.EscapeObjective, out var escapeObjective) && escapeObjective != null)
            AddSharedObjective(first, second, escapeObjective.Value);

        if (_objectives.GetRandomObjective(first.Owner, first.Comp, comp.SharedTargetObjectivePool, float.MaxValue) is { } targetObjective)
            AddSharedObjective(first, second, targetObjective);
    }

    private void AssignSoloObjectives(Entity<MindComponent> mind, BloodBrotherRuleComponent comp)
    {
        if (_objectives.TryCreateObjective(mind, comp.EscapeObjective, out var escapeObjective) && escapeObjective != null)
            _mind.AddObjective(mind.Owner, mind.Comp, escapeObjective.Value);

        if (_objectives.GetRandomObjective(mind.Owner, mind.Comp, comp.SharedTargetObjectivePool, float.MaxValue) is { } targetObjective)
            _mind.AddObjective(mind.Owner, mind.Comp, targetObjective);
    }

    private void AddPartnerObjective(Entity<MindComponent> ownerMind, EntityUid partnerMind)
    {
        if (!_objectives.TryCreateObjective(ownerMind, "BloodBrotherPartnerObjective", out var partnerObjective) || partnerObjective == null)
            return;

        _targetObjective.SetTarget(partnerObjective.Value, partnerMind);
        var partnerName = Name(partnerMind);
        if (TryComp<MindComponent>(partnerMind, out var partnerMindComp))
            partnerName = partnerMindComp.CharacterName ?? Name(partnerMindComp.OwnedEntity ?? partnerMind);

        _metaData.SetEntityName(partnerObjective.Value, Loc.GetString("blood-brother-partner-objective-title", ("targetName", partnerName)));
        _mind.AddObjective(ownerMind.Owner, ownerMind.Comp, partnerObjective.Value);
    }

    private void AddSharedObjective(Entity<MindComponent> first, Entity<MindComponent> second, EntityUid objective)
    {
        _mind.AddObjective(first.Owner, first.Comp, objective);
        _mind.AddObjective(second.Owner, second.Comp, objective);
    }

    private void SendBloodBrotherBriefing(Entity<MindComponent> mind, Entity<MindComponent> brother, BloodBrotherRuleComponent comp)
    {
        if (mind.Comp.UserId == null || !_players.TryGetSessionById(mind.Comp.UserId.Value, out var session))
            return;

        var brotherName = brother.Comp.CharacterName ?? Name(brother.Comp.OwnedEntity ?? brother.Owner);
        var briefing = Loc.GetString("blood-brother-role-greeting", ("brother", brotherName));
        _antag.SendBriefing(session, briefing, BloodBrotherBriefingColor, comp.GreetSoundNotification);
    }

    private void SendSoloBloodBrotherBriefing(Entity<MindComponent> mind, BloodBrotherRuleComponent comp)
    {
        if (mind.Comp.UserId == null || !_players.TryGetSessionById(mind.Comp.UserId.Value, out var session))
            return;

        var briefing = Loc.GetString("blood-brother-role-greeting-solo");
        _antag.SendBriefing(session, briefing, BloodBrotherBriefingColor, comp.GreetSoundNotification);
    }
}
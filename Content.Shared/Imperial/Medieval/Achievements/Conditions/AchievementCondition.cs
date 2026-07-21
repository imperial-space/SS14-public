using System.Linq;
using Content.Shared.Humanoid;
using Content.Shared.Roles.Jobs;
using Content.Shared.Mind.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Roles;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;

namespace Content.Shared.Imperial.Medieval.Achievements;

[ImplicitDataDefinitionForInheritors]
[Serializable, NetSerializable]
public abstract partial class AchievementCondition
{
    [DataField]
    public string ProgressKey { get; private set; } = string.Empty;

    [DataField]
    public int RequiredCount { get; private set; } = 1;

    [DataField]
    public List<string>? RequiredSpecies = null;

    [DataField]
    public List<string>? RequiredJobs = null;

    [DataField]
    public List<ProtoId<MedievalFactionPrototype>>? RequiredFactions = null;

    protected bool CheckFilters(EntityUid player, IEntityManager entManager, IPrototypeManager protoManager)
    {
        if (RequiredSpecies is { Count: > 0 })
        {
            if (!entManager.TryGetComponent<HumanoidAppearanceComponent>(player, out var humanoid))
                return false;

            if (!RequiredSpecies.Contains(humanoid.Species))
                return false;
        }

        if (RequiredJobs is { Count: > 0 })
        {
            var jobSystem = entManager.System<SharedJobSystem>();

            if (!entManager.TryGetComponent<MindContainerComponent>(player, out var mindContainer)
                || mindContainer.Mind == null)
                return false;

            var hasJob = false;
            foreach (var job in RequiredJobs)
            {
                if (jobSystem.MindHasJobWithId(mindContainer.Mind, job))
                {
                    hasJob = true;
                    break;
                }
            }

            if (!hasJob)
                return false;
        }

        if (RequiredFactions is { Count: > 0 })
        {
            if (!entManager.TryGetComponent<MedievalFactionMemberComponent>(player, out var factionMember))
                return false;

            if (!RequiredFactions.Contains(factionMember.Faction))
                return false;
        }

        return true;
    }

    protected void AppendRequirements(FormattedMessage msg, IPrototypeManager proto)
    {
        if (RequiredSpecies is { Count: > 0 })
        {
            var names = RequiredSpecies
                .Select(id => proto.TryIndex<SpeciesPrototype>(id, out var sp) ? Loc.GetString(sp.Name) : id)
                .ToList();
                
            msg.PushColor(Color.FromHex("#7b8e9e"));
            msg.AddText(", " + Loc.GetString("achievement-condition-requires-species", ("species", string.Join(", ", names))));
            msg.Pop();
        }

        if (RequiredJobs is { Count: > 0 })
        {
            var names = RequiredJobs
                .Select(id => proto.TryIndex<JobPrototype>(id, out var job) ? Loc.GetString(job.Name) : id)
                .ToList();

            msg.PushColor(Color.FromHex("#7b8e9e"));
            msg.AddText(", " + Loc.GetString("achievement-condition-requires-jobs", ("jobs", string.Join(", ", names))));
            msg.Pop();
        }

        if (RequiredFactions is { Count: > 0 })
        {
            msg.PushColor(Color.FromHex("#7b8e9e"));
            msg.AddText(", " + Loc.GetString("achievement-condition-requires-faction-header") + " ");
            
            for (var i = 0; i < RequiredFactions.Count; i++)
            {
                var factionId = RequiredFactions[i];
                var name = factionId.ToString();
                var color = Color.White;

                if (proto.TryIndex<MedievalFactionPrototype>(factionId, out var factionProto))
                {
                    name = Loc.GetString(factionProto.Name);
                    color = factionProto.Color;
                }

                msg.PushColor(color);
                msg.AddText(name);
                msg.Pop();

                if (i < RequiredFactions.Count - 1)
                    msg.AddText(", ");
            }
            msg.Pop();
        }
    }

    public abstract FormattedMessage GetDescription(IPrototypeManager protoManager);

    public abstract bool Check(
        EntityUid player,
        IEntityManager entManager,
        IPrototypeManager protoManager,
        object? context,
        Dictionary<string, int> progress);

    public virtual bool TryUpdateProgress(
        EntityUid player,
        IEntityManager entManager,
        IPrototypeManager protoManager,
        object? context,
        Dictionary<string, int> progress)
    {
        return false;
    }

    public virtual int GetCurrentProgress(Dictionary<string, int>? progress)
    {
        if (progress == null || string.IsNullOrEmpty(ProgressKey))
            return 0;

        return progress.GetValueOrDefault(ProgressKey, 0);
    }

    public virtual int GetTargetProgress() => RequiredCount;
}

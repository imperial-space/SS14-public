using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Objectives.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Epsilon;

/// <summary>Оперативник протокола «Эпсилон»: получает цель, как только в тело входит игрок.</summary>
[RegisterComponent]
public sealed partial class ImperialEpsilonOperativeComponent : Component
{
    [DataField]
    public string Objective = "ImperialEpsilonKillCrewObjective";
}

/// <summary>Цель «уничтожить весь экипаж»: в описании счётчик оставшихся живых.</summary>
[RegisterComponent]
public sealed partial class ImperialEpsilonKillCrewConditionComponent : Component
{
    /// <summary>Наибольшее число живого экипажа за время цели — от него считается прогресс.</summary>
    [ViewVariables]
    public int MaxCrew;

    [ViewVariables]
    public int LastRemaining = -1;
}

public sealed class ImperialEpsilonObjectiveSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;

    private static readonly ProtoId<NpcFactionPrototype> DeathSquadFaction = "DeathSquad";

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);
    private TimeSpan _nextRefresh;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialEpsilonOperativeComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<ImperialEpsilonKillCrewConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnMindAdded(Entity<ImperialEpsilonOperativeComponent> ent, ref MindAddedMessage args)
    {
        var mind = args.Mind;
        foreach (var objective in mind.Comp.Objectives)
        {
            if (HasComp<ImperialEpsilonKillCrewConditionComponent>(objective))
                return;
        }

        _mind.TryAddObjective(mind, mind.Comp, ent.Comp.Objective);
        RefreshAll();
    }

    private void OnGetProgress(Entity<ImperialEpsilonKillCrewConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        var remaining = UpdateCondition(ent, CountCrew());
        args.Progress = ent.Comp.MaxCrew == 0 ? 1f : 1f - (float) remaining / ent.Comp.MaxCrew;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextRefresh)
            return;

        _nextRefresh = _timing.CurTime + RefreshInterval;
        RefreshAll();
    }

    private void RefreshAll()
    {
        var query = EntityQueryEnumerator<ImperialEpsilonKillCrewConditionComponent>();
        if (!query.MoveNext(out var first, out var firstComp))
            return;

        var remaining = CountCrew();
        UpdateCondition((first, firstComp), remaining);
        while (query.MoveNext(out var uid, out var comp))
        {
            UpdateCondition((uid, comp), remaining);
        }
    }

    /// <summary>Описание цели — живой счётчик: «Осталось убить: N из M».</summary>
    private int UpdateCondition(Entity<ImperialEpsilonKillCrewConditionComponent> ent, int remaining)
    {
        ent.Comp.MaxCrew = Math.Max(ent.Comp.MaxCrew, remaining);
        if (ent.Comp.LastRemaining == remaining)
            return remaining;

        ent.Comp.LastRemaining = remaining;
        _meta.SetEntityDescription(ent, remaining == 0
            ? Loc.GetString("epsilon-objective-kill-crew-done")
            : Loc.GetString("epsilon-objective-kill-crew-desc", ("remaining", remaining), ("total", ent.Comp.MaxCrew)));
        return remaining;
    }

    /// <summary>Экипаж — живые гуманоиды с разумом игрока, кроме эскадронов смерти.</summary>
    private int CountCrew()
    {
        var count = 0;
        var query = EntityQueryEnumerator<HumanoidProfileComponent, MindContainerComponent>();
        while (query.MoveNext(out var uid, out _, out var container))
        {
            if (!container.HasMind || _mobs.IsDead(uid))
                continue;

            if (HasComp<ImperialEpsilonOperativeComponent>(uid))
                continue;

            if (_faction.IsMember(uid, DeathSquadFaction.Id))
                continue;

            count++;
        }

        return count;
    }
}

using System.Linq;
using Content.Shared.Imperial.Bitrunning;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;

namespace Content.Server.Imperial.Bitrunning;

public enum BitrunningObjectiveKind : byte
{
    /// <summary>Добыть зашифрованный тайник (найти, убить стража или набрать очки).</summary>
    Cache,
    /// <summary>Доставить тайник на точку отправки в убежище.</summary>
    Deliver,
    /// <summary>Доставить диковинки (secondary objectives).</summary>
    Curiosity,
}

/// <summary>Цель аватара в домене: что делать на локации, с живым счётчиком.</summary>
[RegisterComponent]
public sealed partial class BitrunningObjectiveConditionComponent : Component
{
    [DataField]
    public BitrunningObjectiveKind Kind;

    [ViewVariables]
    public EntityUid? Server;

    [ViewVariables]
    public string? LastDescription;
}

/// <summary>
/// Цели в меню персонажа на время подключения: TG объясняет домен окном AvatarHelp и контекстными подсказками,
/// здесь то же самое видно как обычные цели с прогрессом.
/// </summary>
public sealed partial class QuantumServerSystem
{
    [Dependency] private readonly SharedMindSystem _minds = default!;

    private const float FoundRange = 3f;

    private static readonly (BitrunningObjectiveKind Kind, string Proto)[] ObjectivePrototypes =
    {
        (BitrunningObjectiveKind.Cache, "BitrunningObjectiveCache"),
        (BitrunningObjectiveKind.Deliver, "BitrunningObjectiveDeliver"),
        (BitrunningObjectiveKind.Curiosity, "BitrunningObjectiveCuriosity"),
    };

    private void InitializeObjectives()
    {
        SubscribeLocalEvent<BitrunningObjectiveConditionComponent, ObjectiveGetProgressEvent>(OnObjectiveProgress);
    }

    /// <summary>Как добывается тайник в этом домене.</summary>
    private enum CacheMode : byte
    {
        Find,
        Boss,
        Points,
    }

    private CacheMode GetCacheMode(Entity<QuantumServerComponent> ent)
    {
        if (ent.Comp.CacheByPoints)
            return CacheMode.Points;

        return ent.Comp.CacheByBoss ? CacheMode.Boss : CacheMode.Find;
    }

    /// <summary>Подключение: цели домена добавляются разуму битраннера.</summary>
    public void AssignObjectives(EntityUid mindId, Entity<QuantumServerComponent> server)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || !_proto.TryIndex(server.Comp.Domain, out var domain))
            return;

        foreach (var (kind, proto) in ObjectivePrototypes)
        {
            if (kind == BitrunningObjectiveKind.Curiosity && server.Comp.CuriositiesTotal == 0)
                continue;

            if (mind.Objectives.Any(o => TryComp<BitrunningObjectiveConditionComponent>(o, out var c) && c.Kind == kind))
                continue;

            var objective = Spawn(proto);
            var condition = EnsureComp<BitrunningObjectiveConditionComponent>(objective);
            condition.Kind = kind;
            condition.Server = server;
            if (kind == BitrunningObjectiveKind.Cache)
            {
                _meta.SetEntityName(objective, Loc.GetString(GetCacheMode(server) switch
                {
                    CacheMode.Boss => "bitrunning-objective-cache-boss",
                    CacheMode.Points => "bitrunning-objective-cache-points",
                    _ => "bitrunning-objective-cache-find",
                }));
            }

            RefreshObjective((objective, condition), server, domain);
            _minds.AddObjective(mindId, mind, objective);
        }
    }

    /// <summary>Отключение: цели домена снимаются.</summary>
    public void RemoveObjectives(EntityUid mindId)
    {
        if (!TryComp<MindComponent>(mindId, out var mind))
            return;

        for (var i = mind.Objectives.Count - 1; i >= 0; i--)
        {
            if (HasComp<BitrunningObjectiveConditionComponent>(mind.Objectives[i]))
                _minds.TryRemoveObjective(mindId, mind, i);
        }
    }

    /// <summary>Раз в секунду: найден ли тайник и обновление описаний.</summary>
    private void UpdateObjectives(Entity<QuantumServerComponent> ent)
    {
        if (!_proto.TryIndex(ent.Comp.Domain, out var domain))
            return;

        if (!ent.Comp.CacheFound && ent.Comp.DomainGrid is { } grid)
        {
            var caches = EntityQueryEnumerator<BitrunningEncryptedCacheComponent, TransformComponent>();
            while (caches.MoveNext(out var cache, out var comp, out var xform))
            {
                if (!comp.Main || xform.GridUid != grid)
                    continue;

                foreach (var avatar in ent.Comp.Avatars)
                {
                    if (_transform.InRange(Transform(avatar).Coordinates, xform.Coordinates, FoundRange))
                        ent.Comp.CacheFound = true;
                }
            }
        }

        var query = EntityQueryEnumerator<BitrunningObjectiveConditionComponent>();
        while (query.MoveNext(out var uid, out var condition))
        {
            if (condition.Server == ent.Owner)
                RefreshObjective((uid, condition), ent, domain);
        }
    }

    private void RefreshObjective(Entity<BitrunningObjectiveConditionComponent> objective, Entity<QuantumServerComponent> server,
        BitrunningDomainPrototype domain)
    {
        var comp = server.Comp;
        string description;
        switch (objective.Comp.Kind)
        {
            case BitrunningObjectiveKind.Cache:
                var hint = domain.Objective is { } loc ? Loc.GetString(loc) : Loc.GetString("bitrunning-objective-cache-generic");
                description = hint;
                if (GetCacheMode(server) == CacheMode.Points && !comp.CacheSpawned)
                {
                    description += "\n" + Loc.GetString("bitrunning-objective-cache-points-count",
                        ("points", MathF.Floor(comp.MainCratePoints)), ("goal", domain.CratePointGoal));
                }

                if (comp.Complete)
                    description += "\n" + Loc.GetString("bitrunning-objective-cache-delivered");
                else if (comp.CacheSpawned && GetCacheMode(server) != CacheMode.Find)
                    description += "\n" + Loc.GetString("bitrunning-objective-cache-spawned");
                else if (comp.CacheFound)
                    description += "\n" + Loc.GetString("bitrunning-objective-cache-found");
                break;
            case BitrunningObjectiveKind.Deliver:
                description = Loc.GetString(comp.Complete ? "bitrunning-objective-deliver-done" : "bitrunning-objective-deliver-desc");
                break;
            default:
                description = Loc.GetString("bitrunning-objective-curiosity-desc",
                    ("count", comp.CuriositiesDelivered), ("total", comp.CuriositiesTotal));
                break;
        }

        if (objective.Comp.LastDescription == description)
            return;

        objective.Comp.LastDescription = description;
        _meta.SetEntityDescription(objective, description);
    }

    private void OnObjectiveProgress(Entity<BitrunningObjectiveConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        if (!TryComp<QuantumServerComponent>(ent.Comp.Server, out var server) || !_proto.TryIndex(server.Domain, out var domain))
        {
            args.Progress = 0;
            return;
        }

        args.Progress = ent.Comp.Kind switch
        {
            BitrunningObjectiveKind.Deliver => server.Complete ? 1f : 0f,
            BitrunningObjectiveKind.Curiosity => server.CuriositiesTotal == 0
                ? 1f
                : Math.Clamp((float) server.CuriositiesDelivered / server.CuriositiesTotal, 0f, 1f),
            _ => server.Complete ? 1f : GetCacheMode((ent.Comp.Server!.Value, server)) switch
            {
                CacheMode.Points => server.CacheSpawned ? 1f : Math.Clamp(server.MainCratePoints / domain.CratePointGoal, 0f, 1f),
                CacheMode.Boss => server.CacheSpawned ? 1f : 0f,
                _ => server.CacheFound ? 1f : 0f,
            },
        };
    }
}

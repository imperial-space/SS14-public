using System.Linq;
using Content.Shared.Examine;
using Content.Shared.Imperial.Fishing;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Fishing;

/// <summary>
/// Места ловли: компонент ImperialFishingSpot или сопоставление существующих прототипов сборки
/// (вода, лава, пропасть, унитаз и т. п.) с источниками рыбы.
/// </summary>
public sealed class ImperialFishingSpotSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly ImperialFishingSkillSystem _skill = default!;
    [Dependency] private readonly ImperialFishDataSystem _data = default!;

    private const string SpotsId = "Default";

    /// <summary>Кэш: прототип сущности → источник (или null).</summary>
    private readonly Dictionary<string, string?> _byProto = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(_ => _byProto.Clear());
        SubscribeLocalEvent<ImperialFishingSpotComponent, ExaminedEvent>(OnSpotExamined);
    }

    /// <summary>Источник рыбы у сущности, если это место ловли.</summary>
    public ImperialFishSourcePrototype? GetSource(EntityUid uid)
    {
        if (TryComp<ImperialFishingSpotComponent>(uid, out var spot))
            return _proto.Index(spot.Source);

        var sourceId = GetMappedSource(uid);
        return sourceId != null ? _proto.Index<ImperialFishSourcePrototype>(sourceId) : null;
    }

    private string? GetMappedSource(EntityUid uid)
    {
        if (!_proto.TryIndex<ImperialFishingSpotsPrototype>(SpotsId, out var spots))
            return null;

        if (MetaData(uid).EntityPrototype is { } proto)
        {
            if (!_byProto.TryGetValue(proto.ID, out var cached))
            {
                cached = null;
                if (spots.Entities.TryGetValue(proto.ID, out var direct))
                {
                    cached = direct;
                }
                else
                {
                    foreach (var parent in _proto.EnumerateParents<EntityPrototype>(proto.ID))
                    {
                        if (spots.Entities.TryGetValue(parent.ID, out var inherited))
                        {
                            cached = inherited;
                            break;
                        }
                    }
                }

                _byProto[proto.ID] = cached;
            }

            if (cached != null)
                return cached;
        }

        foreach (var (component, source) in spots.Components)
        {
            if (_factory.TryGetRegistration(component, out var registration) && HasComp(uid, registration.Type))
                return source;
        }

        return null;
    }

    /// <summary>Найти место ловли в цели или на клетке (сначала сущность, затем всё на клетке).</summary>
    public EntityUid? FindSpot(EntityUid? target, EntityCoordinates coords, out ImperialFishSourcePrototype? source)
    {
        source = null;
        if (target != null && GetSource(target.Value) is { } targetSource)
        {
            source = targetSource;
            return target;
        }

        var mapCoords = _transform.ToMapCoordinates(coords);
        foreach (var uid in _lookup.GetEntitiesInRange(mapCoords, 0.45f, LookupFlags.Static | LookupFlags.Dynamic | LookupFlags.Sundries))
        {
            if (GetSource(uid) is { } found)
            {
                source = found;
                return uid;
            }
        }

        return null;
    }

    /// <summary>Рыба лежит на своём «родном» месте ловли (associated_safe_turfs).</summary>
    public bool IsOnSafeSpot(Entity<ImperialFishComponent> fish) => GetSafeSpot(fish) != null;

    public EntityUid? GetSafeSpot(Entity<ImperialFishComponent> fish)
    {
        if (MetaData(fish).EntityPrototype is not { } fishProto)
            return null;

        foreach (var uid in _lookup.GetEntitiesInRange(Transform(fish).Coordinates, 0.3f, LookupFlags.Static | LookupFlags.Sundries))
        {
            if (uid == fish.Owner || GetSource(uid) is not { } source)
                continue;

            if (source.Table.ContainsKey(fishProto.ID))
                return uid;
        }

        return null;
    }

    /// <summary>Рыбу выпустили: вернуть в ограниченное количество источника.</summary>
    public void OnFishReleased(EntityUid location, Entity<ImperialFishComponent> fish)
    {
        if (GetSource(location) is { } source)
            EntityManager.System<ImperialFishSourceSystem>().ReaddFish(source, fish);
    }

    private void OnSpotExamined(Entity<ImperialFishingSpotComponent> ent, ref ExaminedEvent args)
    {
        ExamineSpot(ent, args);
    }

    /// <summary>Опытный рыбак видит, кто водится в месте ловли.</summary>
    public void ExamineSpot(EntityUid uid, ExaminedEvent args)
    {
        if (!_skill.CanExamineSpot(args.Examiner) || GetSource(uid) is not { } source)
            return;

        var names = EntityManager.System<ImperialFishSourceSystem>().GetCatchableFishNames(source);
        if (names.Count == 0)
            return;

        args.PushMarkup(Loc.GetString("imperial-fishing-spot-fishes", ("fishes", string.Join(", ", names.Distinct()))));
    }
}

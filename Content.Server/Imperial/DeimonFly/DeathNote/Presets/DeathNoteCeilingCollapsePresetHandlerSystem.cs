using Content.Server.Popups;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Presets;

public sealed class DeathNoteCeilingCollapsePresetHandlerSystem : EntitySystem, IDeathNotePresetHandler
{
    private static readonly ProtoId<DamageTypePrototype> Structural = "Structural";
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public DeathNotePresetHandlerType HandlerType => DeathNotePresetHandlerType.CeilingCollapse;

    public DeathNotePresetExecutionResult Execute(
        in DeathNotePresetExecutionContext context,
        DeathNotePresetParameters parameters)
    {
        if (Deleted(context.Target) ||
            parameters.EntityPrototype is not { } debrisPrototype ||
            !_prototypes.HasIndex<EntityPrototype>(debrisPrototype))
        {
            return DeathNotePresetExecutionResult.Failed("Ceiling collapse target or debris prototype is unavailable.");
        }

        var coordinates = _transform.GetMapCoordinates(context.Target);
        if (coordinates.MapId == MapId.Nullspace)
            return DeathNotePresetExecutionResult.Failed("Ceiling collapse target is in nullspace.");

        if (parameters.MaximumSpawnCount <= 0 ||
            parameters.SpawnCount <= 0 ||
            parameters.SecondarySpawnCount < 0 ||
            (long) parameters.SpawnCount + parameters.SecondarySpawnCount > parameters.MaximumSpawnCount ||
            parameters.SpawnRadius < 0f ||
            parameters.StructuralDamage < 0f ||
            parameters.SecondarySpawnCount > 0 &&
            (parameters.SecondaryEntityPrototype is not { } secondary ||
             !_prototypes.HasIndex<EntityPrototype>(secondary)))
        {
            return DeathNotePresetExecutionResult.Failed(
                "Ceiling debris counts are outside the configured safety limit.");
        }

        if (!DeathNoteDamageHelper.TryCreate(parameters, _random, out var targetDamage))
            return DeathNotePresetExecutionResult.Failed("Ceiling target damage parameters are invalid.");

        var count = parameters.SpawnCount;
        for (var i = 0; i < count; i++)
            Spawn(debrisPrototype, coordinates.Offset(_random.NextVector2(parameters.SpawnRadius)));
        if (parameters.SecondaryEntityPrototype is { } secondaryPrototype)
        {
            for (var i = 0; i < parameters.SecondarySpawnCount; i++)
            {
                Spawn(
                    secondaryPrototype,
                    coordinates.Offset(_random.NextVector2(parameters.SpawnRadius)));
            }
        }

        _popup.PopupEntity(
            Loc.GetString("death-note-ceiling-collapse-popup"),
            context.Target,
            context.Target,
            PopupType.LargeCaution);

        _damageable.TryChangeDamage(
            context.Target,
            targetDamage,
            ignoreResistances: true,
            interruptsDoAfters: true,
            origin: Deleted(context.Writer) ? null : context.Writer,
            ignoreGlobalModifiers: true);

        var nearby = new HashSet<Entity<DamageableComponent>>();
        _lookup.GetEntitiesInRange(coordinates, parameters.SpawnRadius, nearby);
        foreach (var entity in nearby)
        {
            if (entity.Owner == context.Target)
                continue;
            _damageable.TryChangeDamage(entity.Owner, Damage(Structural, parameters.StructuralDamage), origin: context.Target);
        }

        var targetXform = Transform(context.Target);
        if (targetXform.GridUid is { } gridUid &&
            TryComp(gridUid, out MapGridComponent? grid) &&
            _maps.TryGetTileRef(gridUid, grid, targetXform.Coordinates, out var tile) &&
            !tile.Tile.IsEmpty)
        {
            _maps.SetTile(gridUid, grid, tile.GridIndices, Tile.Empty);
        }

        return DeathNotePresetExecutionResult.Succeeded(
            $"Spawned {count} primary and {parameters.SecondarySpawnCount} secondary debris entities, " +
            $"dealt {targetDamage.GetTotal()} configured damage, and breached the target tile.");
    }

    private DamageSpecifier Damage(ProtoId<DamageTypePrototype> type, float amount)
        => new(_prototypes.Index(type), FixedPoint2.New(Math.Max(0f, amount)));
}

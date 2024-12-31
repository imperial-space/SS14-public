using Content.Shared.Explosion.Components;
using Content.Shared.Interaction;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;

namespace Content.Shared.Explosion.EntitySystems;


public abstract class SharedImperialScatteringGrenadeSystem : EntitySystem
{
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialScatteringGrenadeComponent, ComponentInit>(OnScatteringInit);
        SubscribeLocalEvent<ImperialScatteringGrenadeComponent, ComponentStartup>(OnScatteringStartup);
        SubscribeLocalEvent<ImperialScatteringGrenadeComponent, InteractUsingEvent>(OnScatteringInteractUsing);
    }

    private void OnScatteringInit(Entity<ImperialScatteringGrenadeComponent> entity, ref ComponentInit args)
    {
        entity.Comp.Container = _container.EnsureContainer<Container>(entity.Owner, "cluster-payload");
    }

    /// <summary>
    /// Setting the unspawned count based on capacity, so we know how many new entities to spawn
    /// Update appearance based on initial fill amount
    /// </summary>
    private void OnScatteringStartup(Entity<ImperialScatteringGrenadeComponent> entity, ref ComponentStartup args)
    {
        if (entity.Comp.FillPrototype.Count == 0)
            return;

        UpdateAppearance(entity);
        Dirty(entity, entity.Comp);
    }

    /// <summary>
    /// There are some scattergrenades you can fill up with more grenades (like clusterbangs)
    /// This covers how you insert more into it
    /// </summary>
    private void OnScatteringInteractUsing(Entity<ImperialScatteringGrenadeComponent> entity, ref InteractUsingEvent args)
    {
        if (entity.Comp.Whitelist == null)
            return;

        if (args.Handled || !_whitelistSystem.IsValid(entity.Comp.Whitelist, args.Used))
            return;

        _container.Insert(args.Used, entity.Comp.Container);
        UpdateAppearance(entity);
        args.Handled = true;
    }

    /// <summary>
    /// Update appearance based off of total count of contents
    /// </summary>
    private void UpdateAppearance(Entity<ImperialScatteringGrenadeComponent> entity)
    {
        if (!TryComp<AppearanceComponent>(entity, out var appearanceComponent))
            return;

        _appearance.SetData(entity, ClusterGrenadeVisuals.GrenadesCounter, entity.Comp.Container.ContainedEntities.Count, appearanceComponent);
    }
}

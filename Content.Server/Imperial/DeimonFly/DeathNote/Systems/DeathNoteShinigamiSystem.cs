using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Shared.Eye;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Components;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Systems;

/// <summary>
/// Изолирует слой видимости богов смерти от призраков и открывает его только владельцам и держателям тетрадей.
/// </summary>
public sealed class DeathNoteShinigamiSystem : EntitySystem
{
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedVisibilitySystem _visibility = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeathNoteShinigamiComponent, ComponentStartup>(OnShinigamiStartup);
        SubscribeLocalEvent<DeathNoteShinigamiComponent, ComponentShutdown>(OnShinigamiShutdown);
        SubscribeLocalEvent<DeathNoteShinigami2Component, ComponentStartup>(OnShinigami2Startup);
        SubscribeLocalEvent<DeathNoteShinigami2Component, ComponentShutdown>(OnShinigami2Shutdown);
        SubscribeLocalEvent<DeathNoteShinigami3Component, ComponentStartup>(OnShinigami3Startup);
        SubscribeLocalEvent<DeathNoteShinigami3Component, ComponentShutdown>(OnShinigami3Shutdown);

        SubscribeLocalEvent<DeathNoteOwnerComponent, ComponentStartup>(OnOwnerStartup);
        SubscribeLocalEvent<DeathNoteOwnerComponent, GetVisMaskEvent>(OnOwnerGetVisibility);
        SubscribeLocalEvent<DeathNoteOwner2Component, ComponentStartup>(OnOwner2Startup);
        SubscribeLocalEvent<DeathNoteOwner2Component, GetVisMaskEvent>(OnOwner2GetVisibility);
        SubscribeLocalEvent<DeathNoteOwner3Component, ComponentStartup>(OnOwner3Startup);
        SubscribeLocalEvent<DeathNoteOwner3Component, GetVisMaskEvent>(OnOwner3GetVisibility);

        SubscribeLocalEvent<DeathNoteHolderComponent, ComponentStartup>(OnHolderStartup);
        SubscribeLocalEvent<DeathNoteHolderComponent, GetVisMaskEvent>(OnHolderGetVisibility);
        SubscribeLocalEvent<DeathNoteHolder2Component, ComponentStartup>(OnHolder2Startup);
        SubscribeLocalEvent<DeathNoteHolder2Component, GetVisMaskEvent>(OnHolder2GetVisibility);
        SubscribeLocalEvent<DeathNoteHolder3Component, ComponentStartup>(OnHolder3Startup);
        SubscribeLocalEvent<DeathNoteHolder3Component, GetVisMaskEvent>(OnHolder3GetVisibility);
    }

    private void OnShinigamiStartup(Entity<DeathNoteShinigamiComponent> ent, ref ComponentStartup args)
    {
        ApplyShinigamiLayer(ent.Owner, ent.Comp.VisibilityLayer,
            out ent.Comp.HadNormalVisibility, out ent.Comp.AddedVisibilityLayer);
    }

    private void OnShinigamiShutdown(Entity<DeathNoteShinigamiComponent> ent, ref ComponentShutdown args)
    {
        RemoveShinigamiLayer(ent.Owner, ent.Comp.VisibilityLayer,
            ent.Comp.HadNormalVisibility, ent.Comp.AddedVisibilityLayer);
    }

    private void OnShinigami2Startup(Entity<DeathNoteShinigami2Component> ent, ref ComponentStartup args)
    {
        ApplyShinigamiLayer(ent.Owner, ent.Comp.VisibilityLayer,
            out ent.Comp.HadNormalVisibility, out ent.Comp.AddedVisibilityLayer);
    }

    private void OnShinigami2Shutdown(Entity<DeathNoteShinigami2Component> ent, ref ComponentShutdown args)
    {
        RemoveShinigamiLayer(ent.Owner, ent.Comp.VisibilityLayer,
            ent.Comp.HadNormalVisibility, ent.Comp.AddedVisibilityLayer);
    }

    private void OnShinigami3Startup(Entity<DeathNoteShinigami3Component> ent, ref ComponentStartup args)
    {
        ApplyShinigamiLayer(ent.Owner, ent.Comp.VisibilityLayer,
            out ent.Comp.HadNormalVisibility, out ent.Comp.AddedVisibilityLayer);
    }

    private void OnShinigami3Shutdown(Entity<DeathNoteShinigami3Component> ent, ref ComponentShutdown args)
    {
        RemoveShinigamiLayer(ent.Owner, ent.Comp.VisibilityLayer,
            ent.Comp.HadNormalVisibility, ent.Comp.AddedVisibilityLayer);
    }

    private void ApplyShinigamiLayer(EntityUid uid, ushort layer, out bool hadNormal, out bool addedLayer)
    {
        var visibility = EnsureComp<VisibilityComponent>(uid);
        hadNormal = (visibility.Layer & (ushort) VisibilityFlags.Normal) != 0;
        addedLayer = (visibility.Layer & layer) == 0;

        _visibility.AddLayer((uid, visibility), layer, false);
        _visibility.RemoveLayer((uid, visibility), (ushort) VisibilityFlags.Normal, false);
        _visibility.RefreshVisibility(uid, visibility);
    }

    private void RemoveShinigamiLayer(EntityUid uid, ushort layer, bool hadNormal, bool addedLayer)
    {
        if (Terminating(uid) || !TryComp(uid, out VisibilityComponent? visibility))
            return;

        if (addedLayer)
            _visibility.RemoveLayer((uid, visibility), layer, false);

        if (hadNormal)
            _visibility.AddLayer((uid, visibility), (ushort) VisibilityFlags.Normal, false);

        _visibility.RefreshVisibility(uid, visibility);
    }

    private void OnOwnerStartup(Entity<DeathNoteOwnerComponent> ent, ref ComponentStartup args)
    {
        _eye.RefreshVisibilityMask(ent.Owner);
    }

    private void OnOwnerGetVisibility(Entity<DeathNoteOwnerComponent> ent, ref GetVisMaskEvent args)
    {
        if (ent.Comp.LifeStage <= ComponentLifeStage.Running)
            args.VisibilityMask |= DeathNoteVisibilityLayers.Shinigami;
    }

    private void OnOwner2Startup(Entity<DeathNoteOwner2Component> ent, ref ComponentStartup args)
    {
        _eye.RefreshVisibilityMask(ent.Owner);
    }

    private void OnOwner2GetVisibility(Entity<DeathNoteOwner2Component> ent, ref GetVisMaskEvent args)
    {
        if (ent.Comp.LifeStage <= ComponentLifeStage.Running)
            args.VisibilityMask |= DeathNoteVisibilityLayers.Shinigami2;
    }

    private void OnOwner3Startup(Entity<DeathNoteOwner3Component> ent, ref ComponentStartup args)
    {
        _eye.RefreshVisibilityMask(ent.Owner);
    }

    private void OnOwner3GetVisibility(Entity<DeathNoteOwner3Component> ent, ref GetVisMaskEvent args)
    {
        if (ent.Comp.LifeStage <= ComponentLifeStage.Running)
            args.VisibilityMask |= DeathNoteVisibilityLayers.Shinigami3;
    }

    private void OnHolderStartup(Entity<DeathNoteHolderComponent> ent, ref ComponentStartup args)
    {
        _eye.RefreshVisibilityMask(ent.Owner);
    }

    private void OnHolderGetVisibility(Entity<DeathNoteHolderComponent> ent, ref GetVisMaskEvent args)
    {
        if (ent.Comp.LifeStage <= ComponentLifeStage.Running)
            args.VisibilityMask |= DeathNoteVisibilityLayers.Shinigami;
    }

    private void OnHolder2Startup(Entity<DeathNoteHolder2Component> ent, ref ComponentStartup args)
    {
        _eye.RefreshVisibilityMask(ent.Owner);
    }

    private void OnHolder2GetVisibility(Entity<DeathNoteHolder2Component> ent, ref GetVisMaskEvent args)
    {
        if (ent.Comp.LifeStage <= ComponentLifeStage.Running)
            args.VisibilityMask |= DeathNoteVisibilityLayers.Shinigami2;
    }

    private void OnHolder3Startup(Entity<DeathNoteHolder3Component> ent, ref ComponentStartup args)
    {
        _eye.RefreshVisibilityMask(ent.Owner);
    }

    private void OnHolder3GetVisibility(Entity<DeathNoteHolder3Component> ent, ref GetVisMaskEvent args)
    {
        if (ent.Comp.LifeStage <= ComponentLifeStage.Running)
            args.VisibilityMask |= DeathNoteVisibilityLayers.Shinigami3;
    }
}

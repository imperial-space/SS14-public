using Content.Shared.Actions;
using Content.Shared.Hands.Components;
using Content.Shared.Imperial.Lavaland.ColossusLoot;
using Content.Shared.Interaction;
using Content.Shared.Light;
using Content.Shared.Toggleable;

namespace Content.Server.Imperial.Lavaland.ThermalLantern;

public sealed class ThermalLanternSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThermalLanternComponent, ActivateInWorldEvent>(OnActivateInWorld);
        SubscribeLocalEvent<ThermalLanternComponent, ToggleActionEvent>(OnToggleAction);
        SubscribeLocalEvent<ThermalLanternComponent, LightToggleEvent>(OnLightToggle);
        SubscribeLocalEvent<ThermalLanternComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnActivateInWorld(Entity<ThermalLanternComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        ent.Comp.LastActivator = args.User;
    }

    private void OnToggleAction(Entity<ThermalLanternComponent> ent, ref ToggleActionEvent args)
    {
        if (args.Handled)
            return;

        ent.Comp.LastActivator = args.Performer;
    }

    private void OnLightToggle(Entity<ThermalLanternComponent> ent, ref LightToggleEvent args)
    {
        if (!args.IsOn)
        {
            RemoveVision(ent.Comp.VisionUser);
            ent.Comp.VisionUser = null;
            return;
        }

        var activator = ResolveActivator(ent);
        if (activator == null)
            return;

        if (ent.Comp.VisionUser != null && ent.Comp.VisionUser != activator)
            RemoveVision(ent.Comp.VisionUser);

        EnsureComp<ThermalEntityVisionComponent>(activator.Value);
        ent.Comp.VisionUser = activator;
        ent.Comp.LastActivator = activator;
    }

    private void OnShutdown(Entity<ThermalLanternComponent> ent, ref ComponentShutdown args)
    {
        RemoveVision(ent.Comp.VisionUser);
        ent.Comp.VisionUser = null;
    }

    private EntityUid? ResolveActivator(Entity<ThermalLanternComponent> ent)
    {
        if (ent.Comp.LastActivator is { } last && !TerminatingOrDeleted(last))
            return last;

        var parent = Transform(ent).ParentUid;
        if (!parent.IsValid())
            return null;

        if (!TryComp<HandsComponent>(parent, out _))
            return null;

        return parent;
    }

    private void RemoveVision(EntityUid? uid)
    {
        if (uid is not { } target || TerminatingOrDeleted(target))
            return;

        RemComp<ThermalEntityVisionComponent>(target);
    }
}

using Robust.Client.GameObjects;
using Robust.Client.Player;
using Content.Shared.Imperial.LocalLight;
using Robust.Shared.Player;

namespace Content.Client.Imperial.LocalLight;

public sealed class LocalLightSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerMan = default!;
    [Dependency] private readonly PointLightSystem _lightSys = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LocalLightComponent, ComponentInit>(OnLightInit);
        SubscribeLocalEvent<LocalLightComponent, ComponentShutdown>(OnLightShutdown);
        SubscribeLocalEvent<LocalLightComponent, AfterAutoHandleStateEvent>(OnLightSync);
        SubscribeLocalEvent<LocalLightComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LocalLightComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    private void OnLightInit(Entity<LocalLightComponent> ent, ref ComponentInit args)
    {
        SetLightParameters(ent);
    }

    private void OnLightShutdown(Entity<LocalLightComponent> ent, ref ComponentShutdown args)
    {
        RemComp<PointLightComponent>(ent);
    }

    private void OnLightSync(Entity<LocalLightComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        SetLightParameters(ent);
    }

    private void OnPlayerAttached(Entity<LocalLightComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        SetLightParameters(ent);
    }

    private void OnPlayerDetached(Entity<LocalLightComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        SetLightParameters(ent);
    }

    private void SetLightParameters(Entity<LocalLightComponent> ent)
    {
        PointLightComponent clientLight = EnsureComp<PointLightComponent>(ent);

        if (clientLight.NetSyncEnabled)
            clientLight.NetSyncEnabled = false;

        //isn't exactly beautiful but it is what it is
        clientLight.Offset = ent.Comp.Offset;
        _lightSys.SetEnabled(ent, ent.Comp.Enabled && _playerMan.LocalEntity == ent, clientLight);
        _lightSys.SetRadius(ent, ent.Comp.Radius, clientLight);
        _lightSys.SetEnergy(ent, ent.Comp.Energy, clientLight);
        _lightSys.SetSoftness(ent, ent.Comp.Softness, clientLight);
        _lightSys.SetColor(ent, ent.Comp.Color, clientLight);
        _lightSys.SetMask(ent.Comp.MaskPath, clientLight);
        _lightSys.SetCastShadows(ent, ent.Comp.CastShadows, clientLight);
    }
}

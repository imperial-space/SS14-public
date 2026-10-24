using Content.Shared.Imperial.Security;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Security;

/// <summary>shield_icon "shield-old": мерцающий щит поверх носителя.</summary>
public sealed class EnergyShieldVisualsSystem : EntitySystem
{
    private const string LayerKey = "imperial-energy-shield";
    private static readonly SpriteSpecifier.Rsi ShieldSprite = new(new ResPath("Imperial/Security/Gamma/energy_shield.rsi"), "shield");

    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EnergyShieldWearerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<EnergyShieldWearerComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<EnergyShieldWearerComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<EnergyShieldWearerComponent> ent, ref ComponentStartup args)
    {
        Update(ent);
    }

    private void OnState(Entity<EnergyShieldWearerComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        Update(ent);
    }

    private void OnShutdown(Entity<EnergyShieldWearerComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite) && _sprite.LayerMapTryGet((ent, sprite), LayerKey, out var index, false))
            _sprite.LayerSetVisible((ent, sprite), index, false);
    }

    private void Update(Entity<EnergyShieldWearerComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (!_sprite.LayerMapTryGet((ent, sprite), LayerKey, out var index, false))
        {
            index = _sprite.AddLayer((ent, sprite), ShieldSprite);
            _sprite.LayerMapSet((ent, sprite), LayerKey, index);
        }

        _sprite.LayerSetRsiState((ent, sprite), index, ent.Comp.State);
        _sprite.LayerSetVisible((ent, sprite), index, ent.Comp.Active);
    }
}

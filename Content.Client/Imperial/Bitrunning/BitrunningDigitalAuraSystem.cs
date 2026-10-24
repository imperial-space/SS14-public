using Content.Shared.Imperial.Bitrunning;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Bitrunning;

/// <summary>get_digital_overlays(): красный сдвиг и alpha 210 у глюков домена.</summary>
public sealed class BitrunningDigitalAuraSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private static readonly Color Aura = Color.FromHex("#FF8080").WithAlpha(210 / 255f);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BitrunningDigitalAuraComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BitrunningDigitalAuraComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<BitrunningDigitalAuraComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetColor((ent.Owner, sprite), Aura);
    }

    private void OnShutdown(Entity<BitrunningDigitalAuraComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetColor((ent.Owner, sprite), Color.White);
    }
}

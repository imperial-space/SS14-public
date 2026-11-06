using Content.Shared.Imperial.Fugitives;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Fugitives;

/// <summary>Маскировочный имплант беглеца: владелец почти сливается с окружением.</summary>
public sealed class ImperialCamouflageVisualsSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialCamouflagedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ImperialCamouflagedComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<ImperialCamouflagedComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetColor((ent, sprite), sprite.Color.WithAlpha(ent.Comp.Alpha));
    }

    private void OnShutdown(Entity<ImperialCamouflagedComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetColor((ent, sprite), sprite.Color.WithAlpha(1f));
    }
}

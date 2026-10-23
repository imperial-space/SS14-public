using System.Numerics;
using Content.Shared.Imperial.Wizard;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Wizard;

/// <summary>Облик эффектов мага: зелёный гигант после «Мутации».</summary>
public sealed class ImperialWizardVisualsSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private static readonly Color HulkColor = Color.FromHex("#00ff00");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialWizardMutatedComponent, ComponentStartup>(OnMutated);
        SubscribeLocalEvent<ImperialWizardMutatedComponent, ComponentShutdown>(OnUnmutated);
        SubscribeLocalEvent<ImperialTransparentVisualsComponent, ComponentStartup>(OnTransparent);
    }

    private void OnMutated(Entity<ImperialWizardMutatedComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _sprite.SetColor((ent, sprite), HulkColor);
        _sprite.SetScale((ent, sprite), new Vector2(1.25f, 1.25f));
    }

    private void OnTransparent(Entity<ImperialTransparentVisualsComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetColor((ent, sprite), sprite.Color.WithAlpha(0.5f));
    }

    private void OnUnmutated(Entity<ImperialWizardMutatedComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _sprite.SetColor((ent, sprite), Color.White);
        _sprite.SetScale((ent, sprite), Vector2.One);
    }
}

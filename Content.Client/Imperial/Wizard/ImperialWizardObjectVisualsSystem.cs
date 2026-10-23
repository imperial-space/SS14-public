using System.Numerics;
using Content.Shared.Imperial.Wizard;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Client.Imperial.Wizard;

/// <summary>Уменьшение и оживлённые предметы: облик на клиенте.</summary>
public sealed class ImperialWizardObjectVisualsSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ISerializationManager _serMan = default!;

    private readonly Dictionary<EntityUid, string> _drawn = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialShrunkComponent, ComponentStartup>(OnShrunk);
        SubscribeLocalEvent<ImperialShrunkComponent, ComponentShutdown>(OnUnshrunk);
        SubscribeLocalEvent<ImperialAnimatedComponent, AfterAutoHandleStateEvent>(OnAnimated);
        SubscribeLocalEvent<ImperialAnimatedComponent, ComponentShutdown>(OnAnimatedShutdown);
    }

    private void OnShrunk(Entity<ImperialShrunkComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetScale((ent, sprite), new Vector2(0.5f, 0.5f));
    }

    private void OnUnshrunk(Entity<ImperialShrunkComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetScale((ent, sprite), Vector2.One);
    }

    private void OnAnimatedShutdown(Entity<ImperialAnimatedComponent> ent, ref ComponentShutdown args)
    {
        _drawn.Remove(ent);
    }

    private void OnAnimated(Entity<ImperialAnimatedComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Comp.Proto is not { } id || _drawn.TryGetValue(ent, out var drawn) && drawn == id)
            return;

        if (!_proto.TryIndex<EntityPrototype>(id, out var proto) || !proto.TryGetComponent(out SpriteComponent? source, EntityManager.ComponentFactory))
            return;

        _drawn[ent] = id;
        RemComp<SpriteComponent>(ent);
        var dest = AddComp<SpriteComponent>(ent);
        _serMan.CopyTo(source, ref dest, notNullableOverride: true);
    }
}

using Content.Shared.Imperial.StationAi;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Client.Imperial.StationAi;

/// <summary>mimic/copy/machine: appearance = original.appearance — спрайт машины копируется на мимика.</summary>
public sealed class ImperialMalfMimicVisualsSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ISerializationManager _serMan = default!;

    private readonly Dictionary<EntityUid, string> _drawn = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialMalfMimicComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeLocalEvent<ImperialMalfMimicComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<ImperialMalfMimicComponent> ent, ref ComponentShutdown args)
    {
        _drawn.Remove(ent);
    }

    private void OnHandleState(Entity<ImperialMalfMimicComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Comp.Proto is not { } id || _drawn.TryGetValue(ent, out var drawn) && drawn == id)
            return;

        if (!_proto.TryIndex(id, out var proto) || !proto.TryGetComponent(out SpriteComponent? source, EntityManager.ComponentFactory))
            return;

        _drawn[ent] = id;
        RemComp<SpriteComponent>(ent);
        var dest = AddComp<SpriteComponent>(ent);
        _serMan.CopyTo(source, ref dest, notNullableOverride: true);
    }
}

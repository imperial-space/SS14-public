using System.Diagnostics.CodeAnalysis;
using Content.Shared.Imperial.Morph;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Client.Imperial.Morph;

/// <summary>
/// take_form(): user.appearance = form.appearance. Спрайт формы копируется прямо на морфа,
/// как это делает хамелеон-проектор с сущностью-маскировкой, — поэтому клики и удары идут в него самого.
/// </summary>
public sealed class MorphVisualsSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ISerializationManager _serMan = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    /// <summary>Какую версию облика уже нарисовали, чтобы не копировать спрайт каждый тик состояния.</summary>
    private readonly Dictionary<EntityUid, int> _drawnVersion = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MorphComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeLocalEvent<MorphComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<MorphComponent> ent, ref ComponentShutdown args)
    {
        _drawnVersion.Remove(ent);
    }

    private void OnHandleState(Entity<MorphComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_drawnVersion.TryGetValue(ent, out var drawn) && drawn == ent.Comp.FormVersion)
            return;

        _drawnVersion[ent] = ent.Comp.FormVersion;

        var copied = ent.Comp.Morphed
            ? TryGetFormSprite(ent.Comp, out var form) && CopySprite(ent, form)
            : TryGetOwnSprite(ent, out var own) && CopySprite(ent, own);

        // Перезапустить визуализаторы, чтобы у морфа не остались невидимые слои.
        if (copied && TryComp<AppearanceComponent>(ent, out var appearance))
            _appearance.QueueUpdate(ent, appearance);
    }

    private bool TryGetFormSprite(MorphComponent comp, [NotNullWhen(true)] out SpriteComponent? sprite)
    {
        if (TryComp(comp.FormSource, out sprite))
            return true;

        sprite = null;
        return comp.FormProto is { } proto
               && _proto.TryIndex(proto, out var entProto)
               && entProto.TryGetComponent(out sprite, EntityManager.ComponentFactory);
    }

    /// <summary>restore_form(): icon = initial(icon) — спрайт из прототипа самого морфа.</summary>
    private bool TryGetOwnSprite(EntityUid uid, [NotNullWhen(true)] out SpriteComponent? sprite)
    {
        sprite = null;
        return MetaData(uid).EntityPrototype is { } proto
               && proto.TryGetComponent(out sprite, EntityManager.ComponentFactory);
    }

    private bool CopySprite(EntityUid uid, SpriteComponent source)
    {
        // Сначала удалить, потом добавить — иначе остаются чужие слои.
        RemComp<SpriteComponent>(uid);
        var dest = AddComp<SpriteComponent>(uid);
        _serMan.CopyTo(source, ref dest, notNullableOverride: true);
        return true;
    }
}

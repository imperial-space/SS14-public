using Content.Shared.Actions;
using Content.Shared.Construction;
using Content.Shared.DoAfter;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Voidwalker;

/// <summary>Предсказуемый проход сквозь стекло; решётки под током проверяет сервер.</summary>
public abstract class SharedImperialGlassPasserSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> WindowTag = "Window";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialGlassPasserComponent, PreventCollideEvent>(OnPasserCollide);
        SubscribeLocalEvent<ImperialDeformedGlassComponent, PreventCollideEvent>(OnDeformedCollide);
    }

    public bool IsGlass(EntityUid uid)
    {
        return _tag.HasTag(uid, WindowTag) || HasComp<SharedCanBuildWindowOnTopComponent>(uid);
    }

    /// <summary>is_shocked(): решётка под напряжением не пропускает.</summary>
    protected virtual bool IsShocked(EntityUid grille)
    {
        return false;
    }

    private void OnPasserCollide(Entity<ImperialGlassPasserComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled || !IsGlass(args.OtherEntity))
            return;

        if (HasComp<SharedCanBuildWindowOnTopComponent>(args.OtherEntity) && IsShocked(args.OtherEntity))
            return;

        if (ent.Comp.PassTime > TimeSpan.Zero && Timing.CurTime > ent.Comp.PhasingUntil)
            return;

        args.Cancelled = true;
    }

    private void OnDeformedCollide(Entity<ImperialDeformedGlassComponent> ent, ref PreventCollideEvent args)
    {
        if (Timing.CurTime < ent.Comp.Until)
            args.Cancelled = true;
    }
}

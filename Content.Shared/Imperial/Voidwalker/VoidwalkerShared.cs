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

/// <summary>pointed/unsettle.</summary>
public sealed partial class ImperialVoidUnsettleActionEvent : EntityTargetActionEvent;

/// <summary>list_target/telepathy/voidwalker («Космическая передача»).</summary>
public sealed partial class ImperialVoidTelepathyActionEvent : EntityTargetActionEvent;

/// <summary>mob_cooldown/charge/voidwalker («Космический рывок»).</summary>
public sealed partial class ImperialVoidDashActionEvent : WorldTargetActionEvent;

/// <summary>try_convert_wall(): правый клик TG по стене — здесь отдельное действие.</summary>
public sealed partial class ImperialVoidConvertWallActionEvent : EntityTargetActionEvent;

[Serializable, NetSerializable]
public sealed partial class ImperialVoidKidnapDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class ImperialVoidUnsettleDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class ImperialVoidConvertWallDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class ImperialVoidDiveDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class ImperialVoidSurfaceDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class ImperialCosmicSkullDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class ImperialGlassPassDoAfterEvent : SimpleDoAfterEvent;

/// <summary>/datum/brain_trauma/voided: космическая окраска тела.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ImperialVoidedComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color SpaceColor = Color.White;

    /// <summary>/datum/brain_trauma/voided/stable — от космического черепа.</summary>
    [DataField, AutoNetworkedField]
    public bool Stable;
}

/// <summary>/datum/component/glass_passer: сквозь стёкла и решётки (кроме электрифицированных).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialGlassPasserComponent : Component
{
    /// <summary>pass_time: 0 — проходит свободно, иначе нужно «продавиться» сквозь стекло.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan PassTime = TimeSpan.Zero;

    /// <summary>deform_glass: сколько пройденное стекло остаётся проходимым для всех.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan DeformGlass = TimeSpan.FromSeconds(5);

    /// <summary>До этого момента стекло пропускает (после продавливания).</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan PhasingUntil;
}

/// <summary>blomperize(): стекло временно мягкое и пропускает всех.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialDeformedGlassComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Until;
}

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

[Serializable, NetSerializable]
public enum ImperialCosmicSkullVisuals : byte
{
    Drained,
}

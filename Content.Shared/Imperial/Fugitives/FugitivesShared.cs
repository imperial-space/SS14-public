using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fugitives;

/// <summary>item_action/camouflage: включить или выключить маскировку импланта.</summary>
public sealed partial class ImperialCamouflageActionEvent : InstantActionEvent;

/// <summary>pointed/psychic_projection.</summary>
public sealed partial class ImperialPsychicProjectionActionEvent : EntityTargetActionEvent;

/// <summary>charged/psychic_booster.</summary>
public sealed partial class ImperialPsychicBoosterActionEvent : InstantActionEvent;

/// <summary>forcewall/psychic_wall.</summary>
public sealed partial class ImperialPsychicWallActionEvent : InstantActionEvent;

/// <summary>fugitive_capture/mouse_drop_receive(): 5 секунд на заталкивание беглеца.</summary>
[Serializable, NetSerializable]
public sealed partial class ImperialFugitiveCaptureDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Полупрозрачность маскировки (alpha 35 из 255).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialCamouflagedComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Alpha = 35f / 255f;
}

/// <summary>/obj/machinery/fugitive_capture: блюспейс-камера для пойманных беглецов.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialFugitiveCaptureComponent : Component
{
    /// <summary>Вариант псайкеров: сам затягивает беглецов вплотную.</summary>
    [DataField]
    public bool SuckIn;

    [DataField]
    public TimeSpan CaptureTime = TimeSpan.FromSeconds(5);
}

/// <summary>Можно перетащить человека на машину захвата.</summary>
public sealed class ImperialFugitiveCaptureSharedSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialFugitiveCaptureComponent, Content.Shared.DragDrop.CanDropTargetEvent>(OnCanDrop);
    }

    private void OnCanDrop(Entity<ImperialFugitiveCaptureComponent> ent, ref Content.Shared.DragDrop.CanDropTargetEvent args)
    {
        if (!HasComp<Content.Shared.Humanoid.HumanoidProfileComponent>(args.Dragged))
            return;

        args.CanDrop = true;
        args.Handled = true;
    }
}

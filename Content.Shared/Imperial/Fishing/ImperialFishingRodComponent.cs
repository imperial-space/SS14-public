using System.Numerics;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>
/// Удочка (/obj/item/fishing_rod). Наживка, леска и крючок — слоты ItemSlots.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialFishingRodComponent : Component
{
    public const string BaitSlot = "rod_bait";
    public const string LineSlot = "rod_line";
    public const string HookSlot = "rod_hook";

    /// <summary>cast_range.</summary>
    [DataField]
    public int CastRange = 3;

    /// <summary>difficulty_modifier.</summary>
    [DataField]
    public float DifficultyModifier;

    [DataField]
    public float ExperienceMultiplier = 1f;

    [DataField]
    public float CompletionSpeedMult = 1f;

    [DataField]
    public float BaitSpeedMult = 1f;

    [DataField]
    public float DecelerationMult = 1f;

    [DataField]
    public float BouncinessMult = 1f;

    [DataField]
    public float GravityMult = 1f;

    [DataField]
    public float BaitHeightMult = 1f;

    /// <summary>frame_state: рамка мини-игры.</summary>
    [DataField]
    public string FrameState = "frame_wood";

    /// <summary>Телескопическая удочка ловит только разложенной (ItemToggle).</summary>
    [DataField]
    public bool RequiresToggle;

    /// <summary>Наживку нельзя менять (удочка-конструктор с бесконечной наживкой).</summary>
    [DataField]
    public bool LockedBait;

    /// <summary>Описание удочки (ui_description).</summary>
    [DataField]
    public LocId? UiDescription;

    /// <summary>Что сейчас зацеплено леской (для подтягивания).</summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Hooked;

    [ViewVariables]
    public EntityUid? LineVisual;

    [ViewVariables]
    public TimeSpan NextCast;
}

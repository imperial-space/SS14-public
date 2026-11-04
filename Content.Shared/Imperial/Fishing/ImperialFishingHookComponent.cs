using System.Numerics;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Крючок (/obj/item/fishing_hook).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialFishingHookComponent : Component
{
    [DataField]
    public ImperialFishingHookTraits Traits;

    /// <summary>Множитель веса рыбы в таблице (rescue = 0).</summary>
    [DataField]
    public float FishMultiplier = 1f;

    /// <summary>Множитель веса всего, что не рыба (magnet = 5).</summary>
    [DataField]
    public float NonFishMultiplier = 1f;

    /// <summary>TRAIT_ROD_REMOVE_FISHING_DUD.</summary>
    [DataField]
    public bool RemoveDud;

    /// <summary>TRAIT_ROD_ATTRACT_SHINY_LOVERS.</summary>
    [DataField]
    public bool AttractShiny;

    /// <summary>Работает только в пропастях (rescue).</summary>
    [DataField]
    public bool OnlyChasm;

    /// <summary>Может цеплять живых существ (rescue, jaws).</summary>
    [DataField]
    public bool HookMobs;

    /// <summary>rod_overlay_icon_state: наложение крючка на удочку.</summary>
    [DataField]
    public string RodOverlay = "hook_overlay";

    /// <summary>Что вытаскивать из пропасти: Any, Objects, Bodies (chasm_detritus_type).</summary>
    [DataField]
    public string ChasmDetritus = "Any";

    [DataField]
    public LocId? WikiDescription;
}

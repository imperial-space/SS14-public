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

/// <summary>Леска (/obj/item/fishing_line).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialFishingLineComponent : Component
{
    [DataField]
    public ImperialFishingLineTraits Traits;

    [DataField]
    public Color LineColor = Color.Gray;

    [DataField]
    public int CastRange = 2;

    /// <summary>Позволяет ловить в лаве и плазме (TRAIT_ROD_LAVA_USABLE).</summary>
    [DataField]
    public bool LavaUsable;

    [DataField]
    public LocId? WikiDescription;
}

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

/// <summary>Особая блесна: что именно она ловит (is_catchable_fish).</summary>
public enum ImperialLureKind : byte
{
    None,
    Minnow,
    Plug,
    Dropping,
    Spoon,
    ArtificialFly,
    Led,
    LuckyCoin,
    Algae,
    Grub,
    Buzzbait,
    Spinnerbait,
    DaisyChain,
}

/// <summary>
/// Свойства наживки. Любая еда, рыба, наличные, монеты и батареи тоже годятся в наживку,
/// этот компонент задаёт качество и особые свойства.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialFishingBaitComponent : Component
{
    [DataField]
    public ImperialBaitQuality Quality = ImperialBaitQuality.None;

    /// <summary>TRAIT_BAIT_ALLOW_FISHING_DUD.</summary>
    [DataField]
    public bool AllowDud;

    /// <summary>TRAIT_OMNI_BAIT: нравится всем рыбам.</summary>
    [DataField]
    public bool Omni;

    /// <summary>TRAIT_BAIT_UNCONSUMABLE.</summary>
    [DataField]
    public bool Unconsumable;

    /// <summary>TRAIT_POISONOUS_BAIT.</summary>
    [DataField]
    public bool Poisonous;

    /// <summary>spin_frequency блесны в секундах (от, до).</summary>
    [DataField]
    public Vector2? SpinFrequency;

    [DataField]
    public ImperialLureKind Lure;

    /// <summary>Типы пищи TG для этого предмета (если не выводятся из компонентов).</summary>
    [DataField]
    public ImperialFoodTypes FoodTypes;

    /// <summary>Состояние наложения на удочку (rod_overlay_icon_state).</summary>
    [DataField]
    public string? RodOverlay;
}

/// <summary>Место ловли (/datum/component/fishing_spot).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialFishingSpotComponent : Component
{
    [DataField(required: true)]
    public ProtoId<ImperialFishSourcePrototype> Source;
}

/// <summary>Поплавок над местом ловли.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialFishingFloatComponent : Component
{
    /// <summary>Поклёвка: поплавок прыгает сильнее.</summary>
    [DataField, AutoNetworkedField]
    public bool Biting;

    /// <summary>Есть блесна: показывать огонёк.</summary>
    [DataField, AutoNetworkedField]
    public bool HasLure;

    /// <summary>Блесну пора крутить (зелёный огонёк).</summary>
    [DataField, AutoNetworkedField]
    public bool SpinReady;
}

/// <summary>
/// Навык рыбалки (/datum/skill/fishing). Висит на сущности разума.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialFishingSkillComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Experience;
}

/// <summary>
/// Предмет облегчает или усложняет рыбалку, пока надет или в руке
/// (/datum/element/adjust_fishing_difficulty).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialFishingDifficultyModifierComponent : Component
{
    [DataField]
    public float Modifier;

    /// <summary>В каких слотах действует. Руки учитываются, если Hands = true.</summary>
    [DataField]
    public SlotFlags Slots = SlotFlags.NONE;

    [DataField]
    public bool Hands;
}

/// <summary>Банка наживки (/obj/item/bait_can).</summary>
[RegisterComponent]
public sealed partial class ImperialBaitCanComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Bait;

    [DataField]
    public int Uses = 20;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(5);

    [ViewVariables]
    public TimeSpan NextUse;
}

/// <summary>Опознание наживки (favorite_bait / disliked_bait).</summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class ImperialBaitIdentifier
{
    /// <summary>Прототип предмета (с наследниками).</summary>
    [DataField]
    public EntProtoId? Proto;

    /// <summary>Типы пищи.</summary>
    [DataField]
    public ImperialFoodTypes FoodTypes;

    [DataField]
    public ProtoId<ReagentPrototype>? Reagent;

    [DataField]
    public float Amount;

    /// <summary>Особые пометки: "lure" — любая блесна, "fish" — любая рыба.</summary>
    [DataField]
    public string? Special;
}

[Serializable, NetSerializable]
public enum ImperialFishingUiKey : byte
{
    Key,
}

/// <summary>Состояние мини-игры для окна.</summary>
[Serializable, NetSerializable]
public sealed class ImperialFishingMinigameState : BoundUserInterfaceState
{
    public float BaitPosition;
    public float BaitHeight;
    public int BaitPixelHeight;
    public float FishPosition;
    public float Completion;
    public string Background = "background_default";
    public string Frame = "frame_wood";
    public ImperialFishIcon Icon;
    public bool Flip;
    public bool Camo;
    public bool Antigrav;
    public bool Bidirectional;
}

/// <summary>Состояние кнопки мыши: 0 — отпущена, 1 — тянуть вверх, 2 — вниз.</summary>
[Serializable, NetSerializable]
public sealed class ImperialFishingReelMessage : BoundUserInterfaceMessage
{
    public readonly byte State;

    public ImperialFishingReelMessage(byte state)
    {
        State = state;
    }
}

/// <summary>Игрок закрыл окно мини-игры.</summary>
[Serializable, NetSerializable]
public sealed class ImperialFishingGiveUpMessage : BoundUserInterfaceMessage;

/// <summary>Наложения удочки (update_overlays): цвет лески, крючок, наживка.</summary>
[Serializable, NetSerializable]
public enum ImperialFishingRodVisuals : byte
{
    LineColor,
    Hook,
    Bait,
    Collapsed,
}

/// <summary>Состояние катушки удочки (reel_overlay и т. п.).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialFishingRodVisualsComponent : Component
{
    [DataField]
    public string ReelState = "reel_overlay";

    [DataField]
    public Color DefaultLineColor = Color.Gray;

    /// <summary>Состояние разложенной удочки (у телескопических).</summary>
    [DataField]
    public string? ExtendedState;

    /// <summary>Состояние сложенной удочки (у телескопических).</summary>
    [DataField]
    public string? CollapsedState;
}

/// <summary>Записка «совет рыбаку» (/obj/item/paper/paperslip/fishing_tip): случайный совет при создании.</summary>
[RegisterComponent]
public sealed partial class ImperialFishingTipComponent : Component;

/// <summary>
/// Бутылка для послания (glass/bottle + message_in_a_bottle): в неё кладут записку или деньги
/// и бросают в море — послание сохраняется между раундами и может быть выловлено.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialMessageInABottleComponent : Component
{
    public const string SlotId = "message";

    /// <summary>Только что выловлена: подгрузить сохранённое послание.</summary>
    [DataField]
    public bool LoadOnSpawn = true;
}

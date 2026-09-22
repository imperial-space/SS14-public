using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Heretic;

// ─── BUI keys ───────────────────────────────────────────────────────────────

[Serializable, NetSerializable]
public enum HereticInfoBuiKey { Key }

[Serializable, NetSerializable]
public enum HereticRitualBuiKey { Key }

[Serializable, NetSerializable]
public enum HereticTargetBuiKey { Key }

[Serializable, NetSerializable]
public enum HereticFleshWeaveOrganBuiKey { Key }

// ─── FleshWeave BUI state / messages ────────────────────────────────────────

[Serializable, NetSerializable]
public sealed class HereticFleshWeaveOrganData
{
    public NetEntity Organ;
    public string Name = string.Empty;
}

[Serializable, NetSerializable]
public sealed class HereticFleshWeaveOrganBuiState : BoundUserInterfaceState
{
    public List<HereticFleshWeaveOrganData> Organs = new();
    public NetEntity Target;
}

[Serializable, NetSerializable]
public sealed class HereticFleshWeaveSelectOrganMessage : BoundUserInterfaceMessage
{
    public NetEntity Organ;
    public NetEntity Target;
}

// ─── Target BUI state ────────────────────────────────────────────────────────

/// <summary>
/// Snapshot of one named target sent to the client when the target window opens.
/// </summary>
[Serializable, NetSerializable]
public sealed class HereticTargetData
{
    public NetEntity Entity;
    public string Name = string.Empty;
    /// <summary>True if the mob was already Dead at the moment the BUI state was built.</summary>
    public bool IsDead;
}

[Serializable, NetSerializable]
public sealed class HereticTargetBuiState : BoundUserInterfaceState
{
    public List<HereticTargetData> Targets = new();
}

[Serializable, NetSerializable]
public sealed class HereticTargetSelectedMessage : BoundUserInterfaceMessage { }

// ─── Info BUI messages / states ─────────────────────────────────────────────

[Serializable, NetSerializable]
public sealed class HereticInfoBuiState : BoundUserInterfaceState
{
    public int KnowledgePoints;
    public HereticPath CurrentPath;
    public int PassiveLevel;
    public int SacrificeCount;
    public int RequiredSacrifices;
    public int RequiredKnowledge;
    public List<HereticPathData> Paths = new();
    public List<HereticKnowledgeNodeData> Nodes = new();
    public int CurrentShopLevel;
    public List<HereticGiftGroup> PendingGiftGroups = new();
}

[Serializable, NetSerializable]
public sealed class HereticGiftGroup
{
    public string SourceNodeId = string.Empty;
    public List<string> Candidates = new();
}

[Serializable, NetSerializable]
public sealed class HereticKnowledgeNodeData
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public int Cost;
    public HereticPath Path;
    public bool IsResearched;
    public bool PrerequisitesMet;
    public SpriteSpecifier? Icon;
    public List<string> Prerequisites = new();
    public List<List<string>> PrerequisitesAny = new();
    public bool IsGift;
    public int ShopLevel;
    public List<string> ConflictsWith = new();
}

[Serializable, NetSerializable]
public sealed class HereticResearchKnowledgeMessage : BoundUserInterfaceMessage
{
    public string KnowledgeId = string.Empty;
}

[Serializable, NetSerializable]
public sealed class HereticDenyAscensionMessage : BoundUserInterfaceMessage { }

// ─── Ritual BUI messages / states ───────────────────────────────────────────

[Serializable, NetSerializable]
public sealed class HereticRitualBuiState : BoundUserInterfaceState
{
    public List<HereticRitualNodeData>   Rituals  = new();
    public List<HereticOfferingNodeData> Offerings = new();
}

[Serializable, NetSerializable]
public sealed class HereticOfferingNodeData
{
    public NetEntity Target;
    public string Name = string.Empty;
    public int KnowledgeGain;
}

[Serializable, NetSerializable]
public sealed class HereticSelectOfferingMessage : BoundUserInterfaceMessage
{
    public NetEntity Target;
}

[Serializable, NetSerializable]
public sealed class HereticRitualNodeData
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public SpriteSpecifier? Icon;
    public List<HereticRitualIngredientData> Ingredients = new();
}

[Serializable, NetSerializable]
public sealed class HereticRitualIngredientData
{
    public string EntityId = string.Empty;
    public string? Tag;
    public bool HasMobState;
    public bool IsBurning;
    public int Amount = 1;
}

[Serializable, NetSerializable]
public sealed class HereticSelectRitualMessage : BoundUserInterfaceMessage
{
    public string RitualId = string.Empty;
}

// ─── Path data / messages ────────────────────────────────────────────────────

[Serializable, NetSerializable]
public sealed class HereticPathData
{
    public string Id = string.Empty;
    public HereticPath Path;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public string Complexity = string.Empty;
    public string PassiveName = string.Empty;
    public string PassiveDescription = string.Empty;
    public string Pros = string.Empty;
    public string Cons = string.Empty;
    public string Level1Description = string.Empty;
    public string Level2Description = string.Empty;
    public string Level3Description = string.Empty;
    public SpriteSpecifier? Icon;
    public string PathKnowledgeId = string.Empty;
    public string Tips = string.Empty;
}

[Serializable, NetSerializable]
public sealed class HereticSelectPathMessage : BoundUserInterfaceMessage
{
    public string KnowledgeId = string.Empty;
}

// ─── Action events ───────────────────────────────────────────────────────────

public sealed partial class HereticMansusGraspActionEvent : InstantActionEvent { }
public sealed partial class HereticKnowledgeMenuActionEvent : InstantActionEvent { }
public sealed partial class HereticCloakOfShadowActionEvent : InstantActionEvent { }
public sealed partial class HereticDisableCloakActionEvent : InstantActionEvent { }
public sealed partial class HereticHeartbeatMansusActionEvent : InstantActionEvent { }

// Ash
public sealed partial class HereticAshenPassageActionEvent : InstantActionEvent { }
public sealed partial class HereticVolcanoBlastActionEvent : InstantActionEvent { }
public sealed partial class HereticAshlordsRebirthActionEvent : InstantActionEvent { }
public sealed partial class HereticAshSpiritShiftActionEvent : InstantActionEvent { }
public sealed partial class HereticAshSpiritFlameOathActionEvent : InstantActionEvent { }
public sealed partial class HereticScorchedMantleToggleFlamesEvent : InstantActionEvent { }

// Flesh
public sealed partial class HereticImperfectRitualActionEvent      : WorldTargetActionEvent { }
public sealed partial class HereticFleshWeaveActionEvent            : EntityTargetActionEvent { }
public sealed partial class HereticRawProphetJauntActionEvent       : InstantActionEvent { }
public sealed partial class HereticRawProphetBlindActionEvent       : EntityTargetActionEvent { }
public sealed partial class HereticWrithingSenseToggleActionEvent   : InstantActionEvent { }
public sealed partial class HereticShedHumanFormActionEvent          : InstantActionEvent { }

// Flesh — Stalker familiar
public sealed partial class HereticStalkerJauntActionEvent     : InstantActionEvent { }
public sealed partial class HereticStalkerEmpActionEvent       : InstantActionEvent { }
public sealed partial class HereticStalkerPolymorphActionEvent : InstantActionEvent { }

// Void
public sealed partial class HereticVoidPullActionEvent : InstantActionEvent { }
public sealed partial class HereticVoidSeekingBladeActionEvent : EntityTargetActionEvent { }
public sealed partial class HereticVoidCloakToggleHoodEvent : InstantActionEvent { }
public sealed partial class HereticVoidConduitActionEvent : WorldTargetActionEvent { }

// Blade
public sealed partial class HereticRealignmentActionEvent   : InstantActionEvent { }
public sealed partial class HereticSanguineSurgeActionEvent : InstantActionEvent { }
public sealed partial class HereticCleaveActionEvent     : WorldTargetActionEvent { }
public sealed partial class HereticSummonBladesActionEvent : InstantActionEvent { }
public sealed partial class HereticFuriousSteelActionEvent : WorldTargetActionEvent { }
public sealed partial class HereticWolvesAmongSheepActionEvent : WorldTargetActionEvent { }

// Rust
public sealed partial class HereticCorrodeActionEvent    : WorldTargetActionEvent { }
public sealed partial class HereticRustCoatActionEvent   : WorldTargetActionEvent { }
public sealed partial class HereticRustWaveActionEvent   : InstantActionEvent { }
public sealed partial class HereticEntropicPlagueActionEvent : WorldTargetActionEvent { }
public sealed partial class HereticRustingCrownActionEvent : InstantActionEvent { }

// Ash — expanded
public sealed partial class HereticAshlordRiteActionEvent      : InstantActionEvent { }
public sealed partial class HereticFireRingOathActionEvent     : InstantActionEvent { }
public sealed partial class HereticFireCascadeActionEvent      : WorldTargetActionEvent { }

// Moon — Врата Разума
public sealed partial class HereticMoonGateActionEvent          : EntityTargetActionEvent { }
public sealed partial class HereticMoonParadeActionEvent        : WorldTargetActionEvent { }
public sealed partial class HereticMoonRingleaderActionEvent    : InstantActionEvent { }

// Void — expanded
public sealed partial class HereticVoidPhaseActionEvent        : WorldTargetActionEvent { }
public sealed partial class HereticVoidPrisonActionEvent       : EntityTargetActionEvent { }
public sealed partial class HereticWaveOfDesperationActionEvent : InstantActionEvent { }
public sealed partial class HereticMaidInMirrorActionEvent     : InstantActionEvent { }

// Blade — expanded
public sealed partial class HereticRawRitualActionEvent        : InstantActionEvent { }
public sealed partial class HereticStanceOfTornChampionActionEvent : InstantActionEvent { }
public sealed partial class HereticLionhunterRifleActionEvent  : WorldTargetActionEvent { }

// Rust — expanded
public sealed partial class HereticAggressiveSpreadActionEvent : InstantActionEvent { }
public sealed partial class HereticRustConstructionActionEvent : WorldTargetActionEvent { }
public sealed partial class HereticEntropicPlumeActionEvent    : WorldTargetActionEvent { }
public sealed partial class HereticRustDashActionEvent         : WorldTargetActionEvent { }

// Ржавоход — способности фамильяра
public sealed partial class HereticRustWalkerAggressiveSpreadActionEvent : InstantActionEvent { }
public sealed partial class HereticRustWalkerLesserPatrinsReachActionEvent : WorldTargetActionEvent { }

// Ascension — Великий огненный каскад (путь пепла)
public sealed partial class HereticGreatFireCascadeActionEvent : InstantActionEvent { }

// Ascension actions
public sealed partial class HereticAscensionRustActionEvent   : InstantActionEvent { }

// Unsealed Arts — placement of paintings
public sealed partial class HereticUnsealedArtsActionEvent : WorldTargetActionEvent { }

// Sacrifice
public sealed partial class HereticSacrificeActionEvent : WorldTargetActionEvent { }

// Special abilities — «Неистовое сердцебиение»
public sealed partial class HereticRelentlessHeartbeatActionEvent : InstantActionEvent { }

// Familiars — призыв приспешника Мансуса
public sealed partial class HereticSummonFamiliarActionEvent : InstantActionEvent { }

// Maid in Mirror — действие фамилиара
public sealed partial class HereticMaidInMirrorPhaseActionEvent : InstantActionEvent { }

// Maid in Mirror — выход из шара зазеркалья
public sealed partial class HereticMaidMirrorExitActionEvent : InstantActionEvent { }

// General — Поножи Пророка
public sealed partial class HereticGravityToggleActionEvent : InstantActionEvent { }

// General — Нож для высечек (размещение рун)
public sealed partial class HereticCarvingAlertRuneActionEvent : WorldTargetActionEvent { }
public sealed partial class HereticCarvingStunRuneActionEvent : WorldTargetActionEvent { }
public sealed partial class HereticCarvingMadnessRuneActionEvent : WorldTargetActionEvent { }

// General — Космическая руна
public sealed partial class HereticCosmicRuneActionEvent : WorldTargetActionEvent { }

// Cosmos — Звёздный взрыв
public sealed partial class HereticStarBlastActionEvent : WorldTargetActionEvent { }

// Cosmos — Плащ сотканный из звёзд
public sealed partial class HereticCosmosCloakToggleLevitationEvent : InstantActionEvent { }

// Cosmos — Касание Звезды
public sealed partial class HereticStarTouchActionEvent : EntityTargetActionEvent { }

// Cosmos — Космическая экспансия
public sealed partial class HereticCosmicExpansionActionEvent : InstantActionEvent { }

// Cosmos — Переход в фазу
public sealed partial class HereticCosmicPhaseActionEvent : InstantActionEvent { }

// Cosmos — Выход из фазы
public sealed partial class HereticCosmicPhaseExitActionEvent : InstantActionEvent { }

// Cosmos — Вознесение
public sealed partial class HereticAscensionCosmosActionEvent : InstantActionEvent { }

// ─── Star Gazer — способности фамильяра ──────────────────────────────────────

// Созерцатель — Космическая экспансия
public sealed partial class HereticStarGazerCosmicExpansionActionEvent : InstantActionEvent { }

// Созерцатель — Звёздный взрыв (стреляет снарядом / телепорт при повторном нажатии)
public sealed partial class HereticStarGazerStarBlastActionEvent : WorldTargetActionEvent { }

// Созерцатель — Взгляд смерти (луч)
public sealed partial class HereticStarGazerDeathGazeActionEvent : WorldTargetActionEvent { }

// Созерцатель — Найти мастера
public sealed partial class HereticStarGazerFindMasterActionEvent : InstantActionEvent { }

// Lock — Ловкость воришки
public sealed partial class HereticBurglarFinesseActionEvent : EntityTargetActionEvent { }

// Lock — Последнее убежище Хранителя
public sealed partial class HereticCaretakerRefugeActionEvent : InstantActionEvent { }
public sealed partial class HereticCaretakerRefugeExitActionEvent : InstantActionEvent { }

// Lock — Шейпшифт возвышения
public sealed partial class HereticLockShapeshiftActionEvent : InstantActionEvent { }

// ─── DoAfter events ──────────────────────────────────────────────────────────

[Serializable, NetSerializable]
public sealed partial class DrawHereticRuneDoAfterEvent : SimpleDoAfterEvent { }

[Serializable, NetSerializable]
public sealed partial class EraseHereticRuneDoAfterEvent : SimpleDoAfterEvent { }

// Raised on the heretic when Mansus Grasp draws a rune (LKM null-target or Z key)
public sealed class HereticStartSlowRuneDrawEvent : EntityEventArgs { }

[Serializable, NetSerializable]
public sealed partial class AbsorbRealityRiftDoAfterEvent : SimpleDoAfterEvent { }

[Serializable, NetSerializable]
public sealed partial class AbsorbRiftCodexDoAfterEvent : SimpleDoAfterEvent
{
    public NetEntity EffectEntity;
}

[Serializable, NetSerializable]
public sealed partial class HereticSacrificeDoAfterEvent : SimpleDoAfterEvent { }

[Serializable, NetSerializable]
public sealed partial class HereticFleshWeaveDoAfterEvent : SimpleDoAfterEvent
{
    public NetEntity Organ;
    public new NetEntity Target;
}

[Serializable, NetSerializable]
public sealed partial class HereticMaidMirrorEnterDoAfterEvent : SimpleDoAfterEvent { }

// ─── Star Gazer — трекинг курсора (клиент → сервер) ─────────────────────────

[Serializable, NetSerializable]
public sealed class HereticStarGazerCursorUpdateEvent : EntityEventArgs
{
    public NetEntity Uid;
    public MapCoordinates Coords;
    public HereticStarGazerCursorUpdateEvent(NetEntity uid, MapCoordinates coords)
    {
        Uid = uid;
        Coords = coords;
    }
}

using Robust.Shared.Audio;

namespace Content.Server.Imperial.Lavaland.Megafauna;

/// <summary>
/// Общая логика мегафауны SS13 (simple_animal/hostile/megafauna): цель в радиусе агра,
/// OpenFire раз в тик ИИ (SSnpcpool, 2 с), общий откат способностей (MOB_SHARED_COOLDOWN_1),
/// блокировка способностей на время атаки (disable_cooldown_actions) и пожирание трупов (devour).
/// </summary>
[RegisterComponent]
public sealed partial class MegafaunaAiComponent : Component
{
    /// <summary>aggro_vision_range.</summary>
    [DataField]
    public float AggroRange = 18f;

    /// <summary>Период вызова OpenFire (SSnpcpool wait).</summary>
    [DataField]
    public float OpenFireInterval = 2f;

    /// <summary>Пожирать мёртвую цель вплотную: лечит на половину её здоровья.</summary>
    [DataField]
    public bool Devour = true;

    [DataField]
    public LocId DevourMessage = "megafauna-devour";

    [DataField]
    public SoundSpecifier? DeathSound;

    [DataField]
    public LocId? DeathMessage;

    [ViewVariables]
    public EntityUid? Target;

    [ViewVariables]
    public TimeSpan NextOpenFire;

    /// <summary>Общий откат способностей.</summary>
    [ViewVariables]
    public TimeSpan NextAbility;

    /// <summary>Идёт атака — другие способности недоступны.</summary>
    [ViewVariables]
    public bool Busy;

    /// <summary>Босс не может ходить (полёт дракона, телепорт иерофанта, залп колосса).</summary>
    [ViewVariables]
    public bool Immobile;

    /// <summary>Множитель скорости (ярость пузыря, побег с арены дракона).</summary>
    [ViewVariables]
    public float SpeedMultiplier = 1f;

    /// <summary>Неуязвимость (TRAIT_GODMODE во время пике дракона).</summary>
    [ViewVariables]
    public bool Invulnerable;
}

/// <summary>Метка выпотрошенного трупа: мегафауна его больше не трогает (status_effect/gutted).</summary>
[RegisterComponent]
public sealed partial class MegafaunaGuttedComponent : Component;

/// <summary>GiveTarget / LoseTarget.</summary>
[ByRefEvent]
public record struct MegafaunaTargetChangedEvent(EntityUid? OldTarget, EntityUid? NewTarget);

/// <summary>celebrate_kill: босс выпотрошил жертву.</summary>
[ByRefEvent]
public record struct MegafaunaDevourEvent(EntityUid Victim);

/// <summary>Вызывается раз в тик ИИ, если у босса есть цель и способности не заняты (OpenFire).</summary>
[ByRefEvent]
public record struct MegafaunaOpenFireEvent(EntityUid Target, float Distance);

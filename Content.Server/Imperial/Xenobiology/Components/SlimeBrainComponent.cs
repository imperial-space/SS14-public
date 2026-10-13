namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>Режим команд питомца (pet_command): idle/free/follow/attack.</summary>
public enum SlimePetMode : byte
{
    Free,
    Stay,
    Follow,
    Attack,
}

/// <summary>
/// Серверное состояние слайма: blackboard ИИ SS13 (друзья, цели, голод, кормление).
/// </summary>
[RegisterComponent]
public sealed partial class SlimeBrainComponent : Component
{
    /// <summary>BB_FRIENDS_LIST.</summary>
    [ViewVariables]
    public HashSet<EntityUid> Friends = new();

    /// <summary>BB_BASIC_MOB_RETALIATE_LIST / BB_BASIC_MOB_CURRENT_TARGET.</summary>
    [ViewVariables]
    public EntityUid? Target;

    /// <summary>BB_CURRENT_HUNTING_TARGET.</summary>
    [ViewVariables]
    public EntityUid? HuntTarget;

    /// <summary>BB_SLIME_HUNGER_LEVEL.</summary>
    [ViewVariables]
    public Shared.Imperial.Xenobiology.Components.SlimeHunger Hunger;

    /// <summary>buckled: жертва, на которой слайм кормится.</summary>
    [ViewVariables]
    public EntityUid? FeedingOn;

    [ViewVariables]
    public TimeSpan NextLife;

    [ViewVariables]
    public TimeSpan NextHuntSearch;

    [ViewVariables]
    public TimeSpan NextAiTick;

    [ViewVariables]
    public SlimePetMode PetMode = SlimePetMode.Free;

    /// <summary>Друг, за которым следует слайм.</summary>
    [ViewVariables]
    public EntityUid? FollowTarget;

    /// <summary>Друг отдал команду «атаковать» и сейчас укажет цель.</summary>
    [ViewVariables]
    public EntityUid? AttackCommander;

    [ViewVariables]
    public TimeSpan AttackCommandUntil;

    /// <summary>В стазисе от BZ.</summary>
    [ViewVariables]
    public bool BzStasis;

    /// <summary>Замёрз от холода (freon).</summary>
    [ViewVariables]
    public bool Frozen;

    /// <summary>Хранится в консоли (TRAIT_STASIS от консоли).</summary>
    [ViewVariables]
    public bool ConsoleStasis;
}

/// <summary>Друг слайма: его указания пальцем передаются слаймам (pet_command attack).</summary>
[RegisterComponent]
public sealed partial class SlimeCommanderComponent : Component
{
    [ViewVariables]
    public HashSet<EntityUid> Slimes = new();
}

/// <summary>status_effect/slime_leech: на жертве висит кормящийся слайм.</summary>
[RegisterComponent]
public sealed partial class SlimeLeechedComponent : Component
{
    [ViewVariables]
    public EntityUid Slime;

    [ViewVariables]
    public TimeSpan NextTick;
}

/// <summary>status_effect/slime_food: феромоны — слайм, высосавший носителя, подружится с кормильцем.</summary>
[RegisterComponent]
public sealed partial class SlimeFoodComponent : Component
{
    [ViewVariables]
    public EntityUid? Feeder;

    [DataField]
    public float BefriendChance = 100f;
}

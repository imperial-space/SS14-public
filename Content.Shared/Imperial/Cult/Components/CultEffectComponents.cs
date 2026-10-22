using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Cult.Components;

/// <summary>
/// temp_visual/cult/rune_spawn: проявляется (alpha 0 → 255) и сжимается с поворотом за Duration.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CultRuneSpawnEffectComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Duration = 6f;

    /// <summary>turnedness: 179 — против часовой, 181 — по часовой.</summary>
    [DataField, AutoNetworkedField]
    public float Turn = 179f;
}

/// <summary>Луч (Beam) от источника длиной Length тайлов вдоль локального "вверх".</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CultBeamComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Length = 1f;
}

/// <summary>Нимб вознёсшегося культа рисуется клиентом над головой.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CultHaloVisualsComponent : Component;

/// <summary>Кровавая метка мастера (cult_team/blood_target_image): светящийся контур, видимый культистам.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CultBloodTargetComponent : Component;

/// <summary>Hallucinations: кровавые искры над целью (видят только культисты).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CultHorrorMarkComponent : Component
{
    [ViewVariables] public TimeSpan End;
}

/// <summary>Цель «Импульса» мастера выбрана (видит только мастер).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CultPulseSelectedComponent : Component
{
    [ViewVariables, AutoNetworkedField] public EntityUid Master;
}

/// <summary>Окраска/прозрачность спрайта (add_atom_colour, alpha): призраки культа, тёмный дух, тело духа.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CultTintComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color Color = Color.White;
}

/// <summary>
/// Руна Апокалипсиса: не-культисты видят всех культистами/конструктами, культисты — кровавые искры над неверными.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CultApocalypseVisionComponent : Component
{
    [ViewVariables, AutoNetworkedField] public TimeSpan End;

    /// <summary>Образ для не-гуманоидов: wraith/artificer/juggernaut.</summary>
    [ViewVariables, AutoNetworkedField] public string? Construct;
}

/// <summary>Сущность культа, чей спрайт управляется CultVisuals (руны, эффекты, постройки, камни).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CultVisualsComponent : Component
{
    /// <summary>Анимировать вспышку при активации (руны).</summary>
    [DataField]
    public bool Rune;

    /// <summary>Базовое состояние для "_off" у незакреплённых построек.</summary>
    [DataField]
    public string? AnchorState;

    /// <summary>RSI, которым притворяется скрытая рунная дверь.</summary>
    [DataField]
    public string? ConcealedRsi;
}

/// <summary>mob/living/basic/illusion: копирует внешний вид Source.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CultIllusionComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Source;
}

/// <summary>obj/effect/blessing: святая земля, мешает культовой телепортации и фазовому сдвигу.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CultBlessingComponent : Component;

/// <summary>TRAIT_SEE_BLESSED_TILES (призраки-конструкты).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CultSeeBlessedComponent : Component;

/// <summary>wall_walker: жнец проходит сквозь стены культа.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CultWallWalkerComponent : Component
{
    [DataField]
    public List<string> Walls = new() { "WallCult", "WallCultArtificer" };
}

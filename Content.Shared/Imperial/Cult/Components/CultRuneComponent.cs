using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cult.Components;

/// <summary>
/// Тип руны культа.
/// </summary>
[Serializable, NetSerializable]
public enum CultRuneType : byte
{
    Teleport,
    Empowering,
    Offering,
    Revive,
    Barrier,
    Summoning,
    BloodBoil,
    SpiritRealm,
    NarSie,
}

/// <summary>
/// Базовый компонент любой руны культа.
/// Размещается на полу при рисовании ритуальным кинжалом.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class CultRuneComponent : Component
{
    [DataField, AutoNetworkedField]
    public CultRuneType RuneType;

    /// <summary>
    /// Тег телепортационной руны (для руны телепортации).
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? TeleportTag;

    /// <summary>
    /// Скрыта ли руна заклинанием Conceal Presence.
    /// </summary>
    [AutoNetworkedField]
    public bool Concealed;

    /// <summary>
    /// Минимум культистов для активации.
    /// </summary>
    [DataField]
    public int RequiredInvokers = 1;

    /// <summary>
    /// Накопленные заряды (руна воскрешения).
    /// </summary>
    [DataField]
    public int ReviveCharges = 1;

    [DataField]
    public int InvokersOnRune;

    /// <summary>
    /// Состояние спрайта при уничтожении руны (например, анимация rune_large_distorted).
    /// Если задано, перед удалением спрайт переключается в это состояние.
    /// </summary>
    [DataField]
    public string? DestructionState;
}

using Content.Shared.Antag;
using Robust.Shared.GameStates;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;

namespace Content.Shared.Revolutionary.Components;

/// <summary>
/// Component used for marking a Head Rev for conversion and winning/losing.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedRevolutionarySystem))]
public sealed partial class HeadRevolutionaryComponent : Component
{
    /// <summary>
    /// The status icon corresponding to the head revolutionary.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<FactionIconPrototype> StatusIcon { get; set; } = "HeadRevolutionaryFaction";

    /// <summary>
    /// How long the stun will last after the user is converted.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan StunTime = TimeSpan.FromSeconds(3);

    public override bool SessionSpecific => true;

  // Imperial RevaConsent Start
    /// <summary>
    /// Ограничивает конверсию главного революционера, требуя явного согласия целевого персонажа.
    /// Если true - преобразование возможно только через систему подтверждения с участием игрока.
    /// </summary>
    [DataField] public bool OnlyConsentConvert = false;

    /// <summary>
    /// Активирует/деактивирует основную способность конверсии.
    /// При false - функционал преобразования персонажей будет полностью отключен.
    /// </summary>
    [DataField] public bool ConvertAbilityEnabled = true;
    // Imperial RevaConsent End
}

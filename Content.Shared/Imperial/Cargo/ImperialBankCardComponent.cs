using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cargo;

/// <summary>registered_account у ID-карты: номер личного банковского счёта.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialBankCardComponent : Component
{
    [DataField, AutoNetworkedField]
    public int AccountId;
}

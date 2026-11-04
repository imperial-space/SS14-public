using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cargo;

/// <summary>Приложение NT Pay в КПК: личный счёт, история операций, переводы (pay_token TG).</summary>
[RegisterComponent]
public sealed partial class ImperialNtPayCartridgeComponent : Component
{
    /// <summary>Счёт владельца КПК; 0 — берётся счёт ID-карты в КПК.</summary>
    [DataField]
    public int AccountId;
}

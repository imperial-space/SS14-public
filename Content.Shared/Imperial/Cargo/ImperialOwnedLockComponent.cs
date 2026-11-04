using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cargo;

/// <summary>/obj/structure/closet/crate/secure/owned и lockbox/order: открыть может только владелец счёта.</summary>
[RegisterComponent]
public sealed partial class ImperialOwnedLockComponent : Component
{
    [DataField]
    public int AccountId;

    [DataField]
    public string Holder = string.Empty;
}

using Content.Shared.Store;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.SyndieBattle.Components;

[RegisterComponent]
public sealed partial class SyndieBattlePersistentStoreComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<CurrencyPrototype>, int> Balance = new();
}

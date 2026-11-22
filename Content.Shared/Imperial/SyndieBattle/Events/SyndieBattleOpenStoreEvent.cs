using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.SyndieBattle.Events;

[DataDefinition, Serializable, NetSerializable]
public sealed partial class SyndieBattleOpenStoreEvent : CartridgeMessageEvent;

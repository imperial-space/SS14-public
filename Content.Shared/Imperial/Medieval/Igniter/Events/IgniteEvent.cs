using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Igniter;


/// <summary>
/// An event called on an entity to notify it of being set on fire
/// </summary>
[Serializable, NetSerializable]
public sealed partial class IgniteEvent : EntityEventArgs;

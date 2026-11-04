using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Roles.ClownCar;

/// <summary>Запихнуть другого в машину (mob_try_forced_enter).</summary>
[Serializable, NetSerializable]
public sealed partial class ImperialClownCarForceDoAfterEvent : SimpleDoAfterEvent;

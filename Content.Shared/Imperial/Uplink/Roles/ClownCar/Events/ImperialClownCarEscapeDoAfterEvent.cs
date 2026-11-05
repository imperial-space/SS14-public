using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Roles.ClownCar;

/// <summary>Выбраться из багажника (mob_try_exit похищенного).</summary>
[Serializable, NetSerializable]
public sealed partial class ImperialClownCarEscapeDoAfterEvent : SimpleDoAfterEvent;

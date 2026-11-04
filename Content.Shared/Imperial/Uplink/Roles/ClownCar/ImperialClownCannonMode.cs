using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Roles.ClownCar;

public enum ImperialClownCannonMode : byte
{
    Inactive,
    Busy,
    Ready,
}

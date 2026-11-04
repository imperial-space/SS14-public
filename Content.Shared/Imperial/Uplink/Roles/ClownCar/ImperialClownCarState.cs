using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Roles.ClownCar;

[Serializable, NetSerializable]
public enum ImperialClownCarState : byte
{
    Normal,
    ToFire,
    Fire,
    FromFire,
    Recoil,
}

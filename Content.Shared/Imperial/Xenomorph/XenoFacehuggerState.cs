using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Xenomorph;

[Serializable, NetSerializable]
public enum XenoFacehuggerState : byte
{
    Active,
    Idle,
    Dead,
    Impregnated,
}

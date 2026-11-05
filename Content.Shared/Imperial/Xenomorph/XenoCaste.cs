using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Xenomorph;

/// <summary>Касты ксеноморфов (code/modules/mob/living/carbon/alien).</summary>
[Serializable, NetSerializable]
public enum XenoCaste : byte
{
    Larva,
    Hunter,
    Sentinel,
    Drone,
    Praetorian,
    Queen,
}

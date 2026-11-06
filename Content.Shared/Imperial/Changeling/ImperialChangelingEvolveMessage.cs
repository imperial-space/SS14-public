using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Changeling;

[Serializable, NetSerializable]
public sealed class ImperialChangelingEvolveMessage(string power) : BoundUserInterfaceMessage
{
    public readonly string Power = power;
}

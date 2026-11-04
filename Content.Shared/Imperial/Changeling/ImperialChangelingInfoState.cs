using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Changeling;

/// <summary>ui_data() AntagInfoChangeling.</summary>
[Serializable, NetSerializable]
public sealed class ImperialChangelingInfoState(
    string trueName,
    string hiveName,
    int absorbedDna,
    List<string> objectives,
    List<ImperialChangelingMemory> memories,
    string stolenAntagInfo) : BoundUserInterfaceState
{
    public readonly string TrueName = trueName;
    public readonly string HiveName = hiveName;
    public readonly int AbsorbedDna = absorbedDna;
    public readonly List<string> Objectives = objectives;
    public readonly List<ImperialChangelingMemory> Memories = memories;
    public readonly string StolenAntagInfo = stolenAntagInfo;
}

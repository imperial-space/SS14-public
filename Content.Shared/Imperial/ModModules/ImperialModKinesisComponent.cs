using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>kinesis: притягивает предмет к носителю.</summary>
[RegisterComponent]
public sealed partial class ImperialModKinesisComponent : Component
{
    /// <summary>grab_range.</summary>
    [DataField]
    public float Range = 8f;

    [DataField]
    public float ThrowSpeed = 10f;
}

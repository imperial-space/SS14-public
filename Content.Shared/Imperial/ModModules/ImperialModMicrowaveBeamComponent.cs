using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>microwave_beam: разогревает предмет на расстоянии.</summary>
[RegisterComponent]
public sealed partial class ImperialModMicrowaveBeamComponent : Component
{
    [DataField]
    public float Range = 9f;

    /// <summary>Энергия, которую получают растворы предмета, Дж.</summary>
    [DataField]
    public float Heat = 20000f;
}

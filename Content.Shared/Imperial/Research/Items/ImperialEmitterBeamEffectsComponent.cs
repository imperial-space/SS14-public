using Content.Shared.Atmos;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Особые лучи дисков эмиттера TG.</summary>
[RegisterComponent]
public sealed partial class ImperialEmitterBeamEffectsComponent : Component
{
    /// <summary>adjust_fire_stacks у зажигательного луча.</summary>
    [DataField]
    public float FireStacks;

    /// <summary>Рассудок цели (психосифонный луч).</summary>
    [DataField]
    public float Sanity;

    /// <summary>Радиус магнитного вихря (goonchem_vortex).</summary>
    [DataField]
    public float VortexRange;
}

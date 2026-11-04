using Content.Shared.Body;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Imperial.Disease;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Disease;

/// <summary>TRAIT_VIRUSIMMUNE: вид или имплант, не болеющий вирусами.</summary>
[RegisterComponent]
public sealed partial class DiseaseVirusImmuneComponent : Component;

using Content.Shared.Body;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Imperial.Disease;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Disease;

/// <summary>Симптом «Самодыхание»: носителю не нужен воздух.</summary>
[RegisterComponent]
public sealed partial class DiseaseNoBreathComponent : Component;

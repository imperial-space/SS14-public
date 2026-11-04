using Content.Shared.Atmos;
using Content.Shared.Imperial.Fission;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Fission;

/// <summary>Бассейн выдержки: стержни в нём не излучают, мобов тушит и отмывает.</summary>
[RegisterComponent]
public sealed partial class FissionPoolComponent : Component;

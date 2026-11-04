using Content.Shared.Atmos;
using Content.Shared.Imperial.Fission;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Fission;

/// <summary>Выброшенный аварией стержень охладителя (immovablerod/nuclear_rod).</summary>
[RegisterComponent, Access(typeof(FissionSystem))]
public sealed partial class FissionEjectedRodComponent : Component
{
    public const string ContainerId = "fission_ejected";
}

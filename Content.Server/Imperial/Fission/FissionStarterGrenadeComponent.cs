using Content.Shared.Atmos;
using Content.Shared.Imperial.Fission;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Fission;

/// <summary>Нейтронный активатор: запускает камеры в радиусе 3 (nuclear_starter).</summary>
[RegisterComponent, Access(typeof(FissionSystem))]
public sealed partial class FissionStarterGrenadeComponent : Component
{
    [DataField] public float Range = 3;
}

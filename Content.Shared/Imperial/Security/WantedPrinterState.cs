using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Security;

[Serializable, NetSerializable]
public sealed class WantedPrinterState : BoundUserInterfaceState
{
    public List<string> Names = new();
    public List<bool> HasCrimes = new();
    public bool Printing;
}

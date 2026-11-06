using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Bitrunning;

[Serializable, NetSerializable]
public sealed class QuantumConsoleState(
    bool connected,
    string? generatedDomain,
    int occupants,
    int points,
    bool randomized,
    bool ready,
    int scannerTier,
    int retriesLeft,
    List<QuantumConsoleDomainEntry> domains,
    List<QuantumConsoleAvatarEntry> avatars,
    bool broadcasting = false,
    bool broadcastOnCooldown = false) : BoundUserInterfaceState
{
    public bool Broadcasting = broadcasting;
    public bool BroadcastOnCooldown = broadcastOnCooldown;
    public bool Connected = connected;
    public string? GeneratedDomain = generatedDomain;
    public int Occupants = occupants;
    public int Points = points;
    public bool Randomized = randomized;
    public bool Ready = ready;
    public int ScannerTier = scannerTier;
    public int RetriesLeft = retriesLeft;
    public List<QuantumConsoleDomainEntry> Domains = domains;
    public List<QuantumConsoleAvatarEntry> Avatars = avatars;
}

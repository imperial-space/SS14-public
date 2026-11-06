using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Security;

// ─── Секвей (vehicle/ridden/secway) ────────────────────────────────────────

/// <summary>
/// Секвей СБ. Едет только с ключом в замке и без банана в выхлопной трубе.
/// Общий — чтобы скорость считалась одинаково на клиенте и сервере.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SecwayComponent : Component
{
    public const string KeySlot = "secway_key";
    public const string ExhaustContainer = "secway-exhaust";

    [DataField, AutoNetworkedField]
    public bool Blocked = true;

    /// <summary>message_cooldown «глохнет и отказывается ехать».</summary>
    [ViewVariables]
    public TimeSpan NextStallMessage;

    [ViewVariables]
    public TimeSpan NextSmoke;

    /// <summary>Мотор заведён: седок на месте, ключ в замке, труба чистая.</summary>
    [ViewVariables]
    public bool EngineRunning;

    [DataField]
    public TimeSpan InteractUsingDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan VerbsDelay = TimeSpan.FromSeconds(6);

    [DataField]
    public TimeSpan StallMessageDelay = TimeSpan.FromSeconds(0.75);

    [DataField]
    public TimeSpan SmokeDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public float UpdateChance = 0.1f;
}

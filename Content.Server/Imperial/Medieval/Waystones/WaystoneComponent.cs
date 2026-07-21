using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Robust.Shared.Prototypes;

[RegisterComponent]
public sealed partial class WaystoneComponent : Component
{
    [DataField]
    public string Name = "Путеводный камень";

    [DataField]
    public ProtoId<MedievalFactionPrototype>? Faction { get; set; } = string.Empty;

    [DataField]
    public string LinkId = string.Empty;

    [DataField]
    public float TimeToTeleport = 5f;

    [DataField]
    public int DeparturePrice = 15;
    [DataField]
    public int ArrivalPrice = 5;

    [DataField]
    public bool IsEnable = true;

    [DataField]
    public EntityUid? SelectedWaystone;

    [DataField]
    public int CurrentPaid = 0;

    [DataField]
    public EntityUid? User;

    [DataField]
    public TimeSpan BookedTime = TimeSpan.Zero;
    [DataField]
    public float BookedSeconds = 10f;

    public EntityUid? BookedAudioStream;

    public DoAfterId? ActiveDoAfterId;

    [DataField]
    public int CollectedMoney = 0;

    [DataField]
    public float MaxEnergy = 100f;

    [DataField]
    public float CurrentEnergy = 100f;

    [DataField]
    public float EnergyRegenRate = 1f;

    [DataField]
    public string LinkedCircle = string.Empty;
}

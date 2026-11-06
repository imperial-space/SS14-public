using Content.Shared.Atmos;
using Content.Shared.Imperial.Fission;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Fission;

/// <summary>Камера для стержня (reactor_chamber).</summary>
[RegisterComponent, Access(typeof(FissionSystem))]
public sealed partial class FissionChamberComponent : Component
{
    public const string ContainerId = "fission_rod";
    public const float HeatDamage = 15;

    [ViewVariables] public EntityUid? LinkedReactor;
    [ViewVariables] public FissionChamberState State = FissionChamberState.Down;
    [ViewVariables] public bool RequirementsMet;
    [ViewVariables] public bool Operational;
    [ViewVariables] public List<EntityUid> Neighbors = new();
    [ViewVariables] public float HeatTotal;
    [ViewVariables] public float PowerTotal;
    [ViewVariables] public bool Enriching;
    [ViewVariables] public float PowerModTotal = 1;
    [ViewVariables] public float HeatModTotal = 1;
    [ViewVariables] public TimeSpan LockoutUntil;
    [ViewVariables] public int DurabilityLevel;
    [ViewVariables] public bool Welded;
    [ViewVariables] public bool PanelOpen;

    /// <summary>Стержень, вставленный при спавне (uranium / heavy_water подтипы).</summary>
    [DataField] public EntProtoId? StartingRod;

    [DataField] public SoundSpecifier MoveSound = new SoundPathSpecifier("/Audio/Items/deconstruct.ogg");
    [DataField] public SoundSpecifier SwitchSound = new SoundPathSpecifier("/Audio/Machines/machine_switch.ogg");
    [DataField] public SoundSpecifier InsertSound = new SoundPathSpecifier("/Audio/Machines/door_lock_on.ogg");
    [DataField] public SoundSpecifier RemoveSound = new SoundPathSpecifier("/Audio/Machines/door_lock_off.ogg");
    [DataField] public SoundSpecifier WeldSound = new SoundCollectionSpecifier("Welder");
    [DataField] public SoundSpecifier BangSound = new SoundPathSpecifier("/Audio/Effects/explosion_small1.ogg");
    [DataField] public EntProtoId EjectedRod = "ImperialFissionEjectedRod";
}

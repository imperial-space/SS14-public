using Content.Shared.NPC.Prototypes;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.DarkMage.Components;

[RegisterComponent]
public sealed partial class DarkMageComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? Target = null;
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public string Prototype = "MedievalDarkMageObject";
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public string PrototypeFlame = "MedievalDarkMageFlame";
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid Mind;
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan Timing = TimeSpan.FromSeconds(9);
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan TimeToStop = TimeSpan.FromSeconds(20);
    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan LastTiming = TimeSpan.Zero;
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float SearchRadius = 7.5f;
    [ViewVariables(VVAccess.ReadWrite)]
    public bool IsCaptured = false;
    [ViewVariables(VVAccess.ReadWrite)]
    public bool IsMoved = false;
    [ViewVariables(VVAccess.ReadWrite)]
    public bool IsDied = false;
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string ContainerId = "darkMageContainer";
    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid Object;
    [ViewVariables(VVAccess.ReadWrite)]
    public BaseContainer? Container;
    [ViewVariables(VVAccess.ReadWrite)]
    public bool IsFirst = true;
    [ViewVariables(VVAccess.ReadWrite)]
    public HashSet<ProtoId<NpcFactionPrototype>> Faction = ["NanoTrasen"];
    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? Action;
    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid Flame;
}

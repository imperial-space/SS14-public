using Content.Shared.NPC.Prototypes;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Shared.Imperial.Seriozha.SCP.Components;

[RegisterComponent]
public sealed partial class SCP035Component : Component
{
    public bool TryinUneq = false; // fr
    [ViewVariables]
    public EntityUid? In = null;

    #region DamnControl
    public string ContainerId = "SCP035Container";
    public string Prototype = "ImperialSCPMind";
    public EntityUid? Mind;
    public ContainerSlot? Container;
    public EntityUid? Object;
    public TimeSpan ActualTime = TimeSpan.Zero;
    public bool UnderControl = false;
    public Dictionary<EntityUid, bool> Corpses = [];
    public TimeSpan Time = TimeSpan.FromSeconds(60);
    public ProtoId<NpcFactionPrototype> FactionToChange = "SyndicateAgent";
    #endregion
}

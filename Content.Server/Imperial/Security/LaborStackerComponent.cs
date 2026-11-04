using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Security;

/// <summary>stacking_machine/laborstacker: принимает листы и копит очки.</summary>
[RegisterComponent]
public sealed partial class LaborStackerComponent : Component
{
    /// <summary>SHEET_POINT_VALUE.</summary>
    [DataField]
    public int PointsPerSheet = 33;

    /// <summary>gulag_valid: какие листы засчитываются.</summary>
    [DataField]
    public List<ProtoId<StackPrototype>> AcceptedStacks = new();

    [ViewVariables]
    public int Points;

    /// <summary>Сданные листы по типам — отправляются на станцию вместе с освобождённым.</summary>
    [ViewVariables]
    public Dictionary<string, int> Stored = new();
}

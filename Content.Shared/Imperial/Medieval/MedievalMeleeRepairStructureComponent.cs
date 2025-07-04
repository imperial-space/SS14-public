using Robust.Shared.Serialization;
using Content.Shared.DoAfter;

namespace Content.Shared.MedievalMeleeResource.Components;

[RegisterComponent]
public sealed partial class MedievalMeleeRepairStructureComponent : Component
{
    [DataField]
    public float Resource = 10f;
}

[Serializable, NetSerializable]
public sealed partial class MeleeRepairDoAfterEvent : SimpleDoAfterEvent { }

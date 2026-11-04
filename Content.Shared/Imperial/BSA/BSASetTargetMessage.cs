using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.BSA;

/// <summary>«Recalibrate»: навести орудие на маяк.</summary>
[NetSerializable, Serializable]
public sealed class BSASetTargetMessage(NetEntity target) : BoundUserInterfaceMessage
{
    public readonly NetEntity Target = target;
}

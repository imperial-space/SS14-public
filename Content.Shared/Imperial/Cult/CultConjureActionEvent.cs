using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cult;

/// <summary>Призыв предмета/турфа под собой (оболочка, пол, стена, камень душ).</summary>
public sealed partial class CultConjureActionEvent : InstantActionEvent
{
    [DataField]
    public EntProtoId? Prototype;

    /// <summary>Поставить пол культа вместо сущности.</summary>
    [DataField]
    public bool Floor;

    /// <summary>Поставить стену культа.</summary>
    [DataField]
    public bool Wall;
}

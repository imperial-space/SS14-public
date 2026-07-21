using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Forged;

[Serializable, NetSerializable]
public sealed partial class ForgedAssemblyDoAfterEvent : DoAfterEvent
{
    // Вставляем или убираем
    public bool Inserting;
    public string SlotId = string.Empty;

    public override DoAfterEvent Clone() => this;
}

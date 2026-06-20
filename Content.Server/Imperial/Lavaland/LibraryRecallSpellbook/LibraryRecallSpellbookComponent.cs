using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Lavaland.LibraryRecallSpellbook;

[RegisterComponent]
[Access(typeof(LibraryRecallSpellbookSystem))]
public sealed partial class LibraryRecallSpellbookComponent : Component
{
    [DataField]
    public EntProtoId ActionProto = "ActionItemRecall";
}

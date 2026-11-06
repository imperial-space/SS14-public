using Content.Shared.Imperial.MartialArts;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.MartialArts;

[DataDefinition]
public sealed partial class KnownMartialArt
{
    [DataField]
    public ProtoId<MartialArtPrototype> Art;

    /// <summary>origin: если источник удалён или снят — искусство забывается.</summary>
    [DataField]
    public EntityUid? Source;
}

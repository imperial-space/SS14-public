using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Blob.Components;

/// <summary>Заражённый спорой: союзник блоба с панцирем на голове.</summary>
[RegisterComponent]
public sealed partial class BlobInfectedComponent : Component
{
    [ViewVariables] public EntityUid? Overmind;

    /// <summary>Снятые маски столкновений (BlobImpassable) по фикстурам.</summary>
    [ViewVariables] public Dictionary<string, int> DisabledFixtureMasks = new();
}

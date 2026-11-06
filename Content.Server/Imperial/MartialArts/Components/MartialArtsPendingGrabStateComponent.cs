using Content.Shared.Imperial.MartialArts;

namespace Content.Server.Imperial.MartialArts;

/// <summary>
/// Данные <see cref="MartialArtsSystem"/> для этой сущности.
/// </summary>
[RegisterComponent]
public sealed partial class MartialArtsPendingGrabStateComponent : Component
{
    [ViewVariables]
    public Content.Shared.Imperial.MartialArts.MartialGrabState Value;
}

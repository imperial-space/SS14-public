using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.Artifact;

/// <summary>
/// Событие для возврата сознания из животного в оригинальное тело
/// </summary>
public sealed partial class RevertAnimalMindTransferEvent : Content.Shared.Actions.InstantActionEvent
{
}

/// <summary>
/// Компонент для отслеживания животного, в которое перенесено сознание
/// </summary>
[RegisterComponent]
public sealed partial class AnimalMindTransferComponent : Component
{
    /// <summary>
    /// UID оригинального тела, сознание которого перенесено в это животное
    /// </summary>
    [DataField]
    public EntityUid? OriginalBody = null;
}

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Xenobiology;

/// <summary>DoAfter-событие процесса проглатывания ксено-слайма.</summary>
[Serializable, NetSerializable]
public sealed partial class XenoSlimeSwallowDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// Событие, поднимаемое системой инъекций когда реагент попадает в раствор слайма.
/// Обрабатывается XenoSlimeSystem для применения эффектов стабилизатора/стероида/фактора роста.
/// </summary>
public sealed class XenoSlimeInjectedEvent : EntityEventArgs
{
    /// <summary>ID реагента (prototype), который был добавлен.</summary>
    public string ReagentId { get; init; } = string.Empty;
}

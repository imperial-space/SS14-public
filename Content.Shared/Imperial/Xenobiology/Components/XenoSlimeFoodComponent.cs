using Robust.Shared.GameObjects;

namespace Content.Shared.Imperial.Xenobiology.Components;

/// <summary>
/// Маркер: эта сущность является пищей для ксено-слаймов.
/// XenoSlimeSystem записывает сюда последнего атакующего слайма — при переходе в критическое
/// состояние именно он автоматически начинает DoAfter-процесс проглатывания.
/// </summary>
[RegisterComponent]
public sealed partial class XenoSlimeFoodComponent : Component
{
    /// <summary>
    /// Слайм, последним ударивший эту сущность. При переходе в крит/смерть
    /// именно тот слайм запскает DoAfter проглатывания.
    /// </summary>
    public EntityUid? LastAttackerSlime;
}

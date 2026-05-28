using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>
/// Дробилка ксено-слаймов.
/// Включается/выключается кликом. В активном состоянии ищет мёртвых/
/// критических слаймов в радиусе и обрабатывает их с задержкой,
/// издавая звук во время дробления. Экстракт появляется только после
/// полного цикла обработки.
/// </summary>
[RegisterComponent]
public sealed partial class XenoSlimeCrusherComponent : Component
{
    // ── настройки ────────────────────────────────────────────────────────

    /// <summary>Радиус поиска слаймов (в тайлах).</summary>
    [DataField]
    public float AbsorbRange = 2.0f;

    /// <summary>Как часто машина делает новый скан (секунд).</summary>
    [DataField]
    public float ScanInterval = 2.0f;

    /// <summary>Сколько секунд уходит на обработку одного слайма.</summary>
    [DataField]
    public float ProcessTime = 8.0f;

    /// <summary>Звук дробления — играет при добавлении слайма в очередь.</summary>
    [DataField]
    public SoundSpecifier CrushSound = new SoundPathSpecifier("/Audio/Machines/blender.ogg");

    // ── рантайм-состояние ────────────────────────────────────────────────

    /// <summary>Включена ли дробилка.</summary>
    [ViewVariables]
    public bool Active = false;

    /// <summary>Таймер до следующего скана.</summary>
    [ViewVariables]
    public float ScanTimer = 0f;

    /// <summary>
    /// Слаймы в процессе обработки: EntityUid → оставшееся время (сек).
    /// </summary>
    [ViewVariables]
    public readonly Dictionary<EntityUid, float> Processing = new();
}

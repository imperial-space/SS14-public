using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cult;

[Serializable, NetSerializable]
public enum CultVisuals : byte
{
    /// <summary>Счётчик вспышек при активации руны (do_invoke_glow).</summary>
    InvokeGlow,

    /// <summary>Счётчик неудачных активаций (fail_invoke).</summary>
    FailFlash,

    /// <summary>Скрыто заклинанием «Сокрытие присутствия».</summary>
    Concealed,

    /// <summary>Закреплена ли постройка (иначе состояние "_off").</summary>
    Anchored,

    /// <summary>Цвет (руны, ауры).</summary>
    Color,

    /// <summary>Состояние основного слоя.</summary>
    State,
}

using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cult;

/// <summary>Руны культа (obj/effect/rune/*).</summary>
[Serializable, NetSerializable]
public enum CultRuneType : byte
{
    Malformed,
    Offer,
    Empower,
    Teleport,
    NarSie,
    Revive,
    Barrier,
    Summon,
    BloodBoil,
    SpiritRealm,
    Apocalypse,
}

/// <summary>Заклинания магии крови (datum/action/innate/cult/blood_spell/*).</summary>
[Serializable, NetSerializable]
public enum CultSpell : byte
{
    Stun,
    Teleport,
    Emp,
    Shackles,
    Construction,
    Equipment,
    Dagger,
    Horror,
    Veiling,
    BloodRites,
}

[Serializable, NetSerializable]
public enum CultStructureType : byte
{
    None,
    Altar,
    Archives,
    Forge,
    Pylon,
}

[Serializable, NetSerializable]
public enum CultConstructType : byte
{
    Juggernaut,
    Wraith,
    Artificer,
    Harvester,
    Proteon,
    Shade,
}

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

public enum CultVisualLayers : byte
{
    Base,
    Glow,
}

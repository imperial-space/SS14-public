using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Xenobiology;

/// <summary>Особый эффект реакции экстракта (on_reaction в slime_extracts.dm).</summary>
public enum SlimeReactionEffect : byte
{
    None,
    /// <summary>slimespawn: новый слайм цвета экстракта.</summary>
    SpawnSlime,
    /// <summary>slime_rng: радужный — случайный слайм или граната «Blorble Blorble» при 5+ ед.</summary>
    RandomSlimeOrBomb,
    /// <summary>slimebomb: нестабильная граната из радужного экстракта.</summary>
    VolatileBomb,
    /// <summary>slimefoam: пена с содержимым экстракта.</summary>
    Foam,
    /// <summary>slimefreeze: облако азота 2.7 K через 5 с.</summary>
    Freeze,
    /// <summary>slimefire: облако плазмы 1000 K через 5 с.</summary>
    Fire,
    /// <summary>slimeoverload: ЭМИ 3/5.</summary>
    Emp,
    /// <summary>slimeexplosion: взрыв 1/2/4 через 5 с.</summary>
    Explosion,
    /// <summary>slimebloodlust: слаймы вокруг впадают в ярость.</summary>
    Bloodlust,
    /// <summary>slimestop: остановка времени через 5 с.</summary>
    Timestop,
    /// <summary>slimemobspawn: существа через 5 с.</summary>
    MobSpawn,
    /// <summary>slimebork: случайная еда.</summary>
    RandomFood,
    /// <summary>slimebork/drinks: случайные напитки.</summary>
    RandomDrinks,
    /// <summary>slimecrayon: случайный мелок.</summary>
    RandomCrayon,
}

/// <summary>
/// datum/chemical_reaction/slime: реакция реагентов внутри экстракта слайма.
/// </summary>
[Prototype]
public sealed partial class SlimeReactionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>required_container: экстракт какого цвета.</summary>
    [DataField(required: true)]
    public ProtoId<SlimeTypePrototype> Extract;

    /// <summary>required_reagents.</summary>
    [DataField(required: true)]
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> Reactants = new();

    /// <summary>results: реагенты, появляющиеся в экстракте (на каждую единицу реакции).</summary>
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> Products = new();

    /// <summary>Сущности, появляющиеся рядом (прототип → количество штук).</summary>
    [DataField]
    public Dictionary<EntProtoId, int> Spawn = new();

    /// <summary>Стаки (прототип → размер стака).</summary>
    [DataField]
    public Dictionary<EntProtoId, int> Stacks = new();

    [DataField]
    public SlimeReactionEffect Effect = SlimeReactionEffect.None;

    /// <summary>Задержка эффекта, секунды (вибрирующий экстракт).</summary>
    [DataField]
    public float Delay;

    /// <summary>Сообщение при начале реакции.</summary>
    [DataField]
    public LocId? Message;

    /// <summary>deletes_extract: экстракт удаляется сразу, если кончились заряды.</summary>
    [DataField]
    public bool DeletesExtract = true;

    /// <summary>REACTION_TAG_DANGEROUS: пишется в админский лог.</summary>
    [DataField]
    public bool Dangerous;

    // ─── MobSpawn ───

    /// <summary>Сколько существ.</summary>
    [DataField]
    public int MobCount = 1;

    /// <summary>HOSTILE_SPAWN / FRIENDLY_SPAWN.</summary>
    [DataField]
    public bool HostileMobs = true;

    /// <summary>Конкретное существо вместо случайного (паук-матка).</summary>
    [DataField]
    public EntProtoId? MobPrototype;

    /// <summary>Существа становятся нейтральными (FACTION_NEUTRAL).</summary>
    [DataField]
    public bool NeutralMobs;
}

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

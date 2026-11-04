using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Genetics.Prototypes;

public enum MeltdownAction : byte
{
    None,

    /// <summary>Превращение в обезьяну через обезьяний ген.</summary>
    Monkey,

    /// <summary>Урон ×200 до конца жизни (damage_resistance −20000).</summary>
    Fragile,

    /// <summary>Медленный клеточный распад (decloning).</summary>
    Decloning,

    /// <summary>Отбрасывает в случайную сторону (go_away).</summary>
    Yeet,

    /// <summary>Ноги отказывают навсегда (paraplegic).</summary>
    Paraplegic,

    Gib,

    /// <summary>Тело рассыпается в прах.</summary>
    Dust,
}

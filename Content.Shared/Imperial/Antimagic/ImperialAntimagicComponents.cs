using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Antimagic;

/// <summary>
/// Вид магии и защиты от неё (MAGIC_RESISTANCE / MAGIC_RESISTANCE_HOLY из SS13).
/// </summary>
[Flags]
public enum ImperialMagicResistance : byte
{
    None = 0,

    /// <summary>Обычная магия: её блокирует антимагия нулевого стержня.</summary>
    Magic = 1 << 0,

    /// <summary>Нечестивая магия культа: её блокирует ещё и святая вода в теле.</summary>
    Holy = 1 << 1,

    All = Magic | Holy,
}

/// <summary>
/// Предмет даёт антимагию тому, кто держит его в руках или носит (anti_magic у nullrod_core).
/// Носитель не может сам колдовать магию, от которой защищён.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialAntimagicComponent : Component
{
    [DataField]
    public ImperialMagicResistance Resistance = ImperialMagicResistance.All;
}

/// <summary>
/// Действие — заклинание еретика или культа. Его нельзя применить, держа антимагию,
/// а заклинание с целью не действует на цель под защитой.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialMagicActionComponent : Component
{
    [DataField]
    public ImperialMagicResistance Resistance = ImperialMagicResistance.Magic;
}

/// <summary>
/// В теле святая вода (TRAIT_HOLY из SS13). Сколько секунд она уже действует — для порогов эффектов.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialHolyWaterComponent : Component
{
    [ViewVariables]
    public float Seconds;

    [ViewVariables]
    public TimeSpan NextSpellClear;
}

/// <summary>
/// Поднимается на заклинателе перед применением заклинания с <see cref="ImperialMagicActionComponent"/>.
/// Антагонист может отменить своё колдовство, например еретик со святой водой в теле.
/// </summary>
[ByRefEvent]
public record struct ImperialMagicCastAttemptEvent(EntityUid User, bool Cancelled = false);

/// <summary>
/// Нечестивая руна, которую стирает удар святого оружия (effect_remover у nullrod_core в SS13).
/// </summary>
[RegisterComponent]
public sealed partial class ImperialNullRodErasableComponent : Component;

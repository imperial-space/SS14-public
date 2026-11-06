namespace Content.Shared.Imperial.Heretic.Core;

/// <summary>
/// Метка пути еретика на цели. Спадает сама, если еретик долго не обновлял её Хваткой Мансуса.
/// </summary>
public interface IHereticMarkComponent
{
    /// <summary>Сколько метка держится после наложения.</summary>
    TimeSpan Lifetime { get; }

    /// <summary>Когда метка спадёт.</summary>
    TimeSpan ExpireTime { get; set; }
}

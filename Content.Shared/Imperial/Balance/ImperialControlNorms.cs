namespace Content.Shared.Imperial.Balance;

/// <summary>
/// Перевод длительностей контроля из TG в масштаб SS14.
/// В SS14 жёсткий контроль короткий: вспышка в упор 1.5 с, стамкрит после трёх ударов дубинкой 6 с.
/// Используют боевые искусства и предметы аплинка с числами из TG.
/// </summary>
public static class ImperialControlNorms
{
    /// <summary>Паралич и оглушение: до 2 с без изменений, дальше ×0.25 в пределах 2–3 с.</summary>
    public static TimeSpan HardStun(TimeSpan tg)
    {
        if (tg <= TimeSpan.FromSeconds(2))
            return tg;

        return TimeSpan.FromSeconds(Math.Clamp(tg.TotalSeconds * 0.25, 2, 3));
    }

    /// <summary>Сбивание с ног: ×0.3 в пределах 1–4 с.</summary>
    public static TimeSpan Knockdown(TimeSpan tg)
    {
        return TimeSpan.FromSeconds(Math.Clamp(tg.TotalSeconds * 0.3, Math.Min(1, tg.TotalSeconds), 4));
    }

    /// <summary>Принудительный сон (нокаут, удушение, снотворное): ×0.25 в пределах 2–10 с.</summary>
    public static TimeSpan Sleep(TimeSpan tg)
    {
        return TimeSpan.FromSeconds(Math.Clamp(tg.TotalSeconds * 0.25, Math.Min(2, tg.TotalSeconds), 10));
    }
}

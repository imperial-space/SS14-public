namespace Content.Shared.Imperial.DeimonFly.DeathNote;

/// <summary>
/// Слои видимости модуля Death Note.
/// Перед реализацией видимости свободный бит необходимо повторно проверить на актуальной ветке.
/// </summary>
public static class DeathNoteVisibilityLayers
{
    /// <summary>
    /// На момент повторной проверки ветки events биты 0–4 заняты.
    /// Модуль резервирует отдельные биты 6–8 для трёх независимых каналов богов смерти.
    /// </summary>
    public const ushort Shinigami = 1 << 6;
    public const ushort Shinigami2 = 1 << 7;
    public const ushort Shinigami3 = 1 << 8;
}

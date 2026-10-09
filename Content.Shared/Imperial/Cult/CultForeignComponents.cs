namespace Content.Shared.Imperial.Cult;

/// <summary>
/// Компоненты других фич (еретик, священник), с которыми культ взаимодействует.
/// Проверяются по зарегистрированному имени, чтобы культ не зависел от их сборки:
/// если фичи нет, проверка просто возвращает false.
/// </summary>
public static class CultForeignComponents
{
    /// <summary>HereticComponent: еретик.</summary>
    public const string Heretic = "Heretic";

    /// <summary>ImperialHolyComponent: святой (священник), TRAIT_HOLY.</summary>
    public const string Holy = "ImperialHoly";

    public static bool Has(IEntityManager entMan, EntityUid uid, string componentName)
    {
        return entMan.ComponentFactory.TryGetRegistration(componentName, out var registration) &&
               entMan.HasComponent(uid, registration.Type);
    }
}

namespace Content.Server.Imperial.Cult.Components;

/// <summary>
/// Компонент ритуального кинжала.
/// Используется для рисования рун, удаления рун, сбора святой воды с культистов,
/// якорения/разякорения культовых структур.
/// </summary>
[RegisterComponent]
public sealed partial class CultDaggerComponent : Component
{
    /// <summary>
    /// Время на рисование руны (в секундах).
    /// </summary>
    [DataField]
    public float DrawTime = 6f;

    /// <summary>
    /// Урон, наносимый при рисовании (собственное HP).
    /// </summary>
    [DataField]
    public float SelfDamage = 5f;

    /// <summary>
    /// Рунный металл, который нужно иметь в рукоятке для постройки структуры.
    /// </summary>
    [DataField]
    public string RunedMetalPrototype = "CultRunedMetal";
}

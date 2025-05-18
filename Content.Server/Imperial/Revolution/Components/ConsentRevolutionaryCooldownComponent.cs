namespace Content.Server.Imperial.Revolutionary.Components
{
    /// <summary>
    /// Временный компонент, блокирующий повторные попытки обращения в революционеры.
    /// Добавляется после обращения для предотвращения спама и злоупотреблений.
    /// </summary>
    [RegisterComponent]
    public sealed partial class ConsentRevolutionaryCooldownComponent : Component
    {
    }
}

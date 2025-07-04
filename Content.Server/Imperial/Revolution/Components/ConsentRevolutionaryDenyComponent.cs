namespace Content.Server.Imperial.Revolutionary.Components
{
    /// <summary>
    /// Компонент, блокирующий возможность обращения сущности в революционеры.
    /// Используется для постоянной защиты от нежелательных обращений.
    /// </summary>
    [RegisterComponent]
    public sealed partial class ConsentRevolutionaryDenyComponent : Component
    {
        /// <summary>
        /// Локализованный ключ сообщения, отображаемого инициатору при попытке обращения.
        /// </summary>
        [DataField]
        public string OnConversionAttemptText = "rev-consent-convert-failed-convert-block";
    }
}

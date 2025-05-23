using Content.Server.Imperial.Revolutionary.UI;

namespace Content.Server.Imperial.Revolutionary.Components
{
    /// <summary>
    /// Компонент, отвечающий за состояние обращения сущности в революционеры с согласием.
    /// </summary>
    [RegisterComponent]
    public sealed partial class ConsentRevolutionaryComponent : Component
    {
        /// <summary>
        /// Сущность, являющаяся другой стороной запроса на обращение.
        /// Если null, значит запрос отсутствует.
        /// </summary>
        [DataField]
        public EntityUid? OtherMember;

        /// <summary>
        /// Флаг, указывающий, является ли сущность инициатором обращения.
        /// Если false, сущность является целью обращения.
        /// </summary>
        [DataField]
        public bool IsConverter = false;

        /// <summary>
        /// Окно интерфейса подтверждения обращения.
        /// </summary>
        public ConsentRequestedEui? Window;

        /// <summary>
        /// Время начала текущего запроса на обращение.
        /// </summary>
        [DataField]
        public TimeSpan? RequestStartTime;

        /// <summary>
        /// Максимальное время ожидания ответа на запрос обращения.
        /// </summary>
        [DataField]
        public TimeSpan ResponseTime = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Время блокировки повторных запросов после отказа.
        /// </summary>
        [DataField]
        public TimeSpan RequestBlockTime = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Время блокировки новых запросов после успешного обращения.
        /// </summary>
        [DataField]
        public TimeSpan ConversionBlockTime = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Максимальная дистанция для взаимодействия при запросе обращения.
        /// </summary>
        [DataField]
        public float MaxDistance = 3f;
    }
}

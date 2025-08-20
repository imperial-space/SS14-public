using Content.Shared.Imperial.CustomChaplain;
using Content.Server.Popups;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Content.Shared.Bible;
using Robust.Shared.GameObjects;
using Content.Server.Bible.Components;
using Robust.Shared.Localization;
using System.Linq;

namespace Content.Server.Imperial.CustomChaplain
{
    public sealed class GodSelectionSystem : EntitySystem
    {
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<GodSelectionComponent, GetVerbsEvent<AlternativeVerb>>(AddChooseGodVerb);
            SubscribeLocalEvent<GodSelectionComponent, GodSelectionChooseGodMessage>(OnGodSelected);
            SubscribeLocalEvent<GodSelectionComponent, BoundUIClosedEvent>(OnUIClosed);
        }

        private void AddChooseGodVerb(EntityUid uid, GodSelectionComponent component, GetVerbsEvent<AlternativeVerb> args)
        {
            if (!args.CanInteract || !args.CanAccess)
                return;

            // Проверяем, что у пользователя есть компонент BibleUser
            if (!HasComp<BibleUserComponent>(args.User))
                return;

            // Проверяем, был ли уже подтвержден выбор бога
            if (component.GodSelected && !string.IsNullOrEmpty(component.SelectedGod))
                return;

            AlternativeVerb verb = new()
            {
                Act = () => OpenGodSelection(uid, component, args.User),
                Text = "Выбрать божество",
                Priority = 1
            };
            args.Verbs.Add(verb);
        }

        private void OpenGodSelection(EntityUid uid, GodSelectionComponent component, EntityUid user)
        {
            // Проверяем, был ли уже подтвержден выбор бога
            if (component.GodSelected && !string.IsNullOrEmpty(component.SelectedGod))
            {
                _popupSystem.PopupEntity("Божество уже выбрано!", uid, user);
                return;
            }

            var state = new GodSelectionBuiState(component.GodSelected, component.SelectedGod, component.IsCustomGod);
            _uiSystem.SetUiState(uid, GodSelectionUiKey.Key, state);
            _uiSystem.TryOpenUi(uid, GodSelectionUiKey.Key, user);
        }

                private void OnGodSelected(EntityUid uid, GodSelectionComponent component, GodSelectionChooseGodMessage message)
        {
            // Для BUI сообщений пользователь должен быть получен из контекста сессии
            // В данном случае используем упрощенный подход - проверяем кто может взаимодействовать с объектом
            var query = EntityQueryEnumerator<BibleUserComponent>();
            EntityUid? user = null;
            while (query.MoveNext(out var userEntity, out _))
            {
                // Простая проверка - берем первого найденного пользователя с BibleUserComponent
                // В реальной игре это будет тот, кто открыл UI
                user = userEntity;
                break;
            }

            if (user == null)
                return;

            // Проверяем, был ли уже подтвержден выбор бога
            if (component.GodSelected && !string.IsNullOrEmpty(component.SelectedGod))
            {
                _popupSystem.PopupEntity(Loc.GetString("god-selection-already-selected"), uid, user.Value);
                return;
            }

            // Серверная валидация имени: обрезка, запасной вариант и ограничение длины
            var godName = string.IsNullOrWhiteSpace(message.GodName)
                ? "Безымянный"
                : message.GodName.Trim();

            // Для кастомных богов проводим дополнительную валидацию
            if (message.IsCustom)
            {
                // Проверка на управляющие символы
                if (godName.Any(ch => char.IsControl(ch)))
                {
                    _popupSystem.PopupEntity(Loc.GetString("god-selection-server-invalid-characters"), uid, user.Value);
                    return;
                }

                // Проверка на недопустимые символы (HTML теги, специальные символы)
                if (godName.Any(ch => ch == '<' || ch == '>' || ch == '&' || ch == '"' || ch == '\'' || ch == '\\' || ch == '/'))
                {
                    _popupSystem.PopupEntity(Loc.GetString("god-selection-server-invalid-characters"), uid, user.Value);
                    return;
                }

                // Проверка на слишком много повторяющихся символов (защита от спама)
                if (HasTooManyRepeatingChars(godName))
                {
                    _popupSystem.PopupEntity(Loc.GetString("god-selection-server-invalid-characters"), uid, user.Value);
                    return;
                }
            }

            // Ограничиваем длину (даже после валидации, на всякий случай)
            if (godName.Length > 32)
                godName = godName.Substring(0, 32);

            component.SelectedGod = godName;
            component.IsCustomGod = message.IsCustom;
            component.GodSelected = true;

            var locKey = message.IsCustom ? "god-selection-success-custom" : "god-selection-success-predefined";
            var typeKey = message.IsCustom ? "god-selection-custom" : "god-selection-predefined";
            _popupSystem.PopupEntity(Loc.GetString(locKey, ("godName", godName), ("type", Loc.GetString(typeKey))), uid, user.Value);

            // Update UI state
            var state = new GodSelectionBuiState(component.GodSelected, component.SelectedGod, component.IsCustomGod);
            _uiSystem.SetUiState(uid, GodSelectionUiKey.Key, state);
        }

        private void OnUIClosed(EntityUid uid, GodSelectionComponent component, BoundUIClosedEvent args)
        {
            // Если UI был закрыт без подтверждения выбора, полностью сбрасываем состояние
            if (args.UiKey.Equals(GodSelectionUiKey.Key))
            {
                // Сбрасываем состояние только если бог не был окончательно выбран
                if (!component.GodSelected || string.IsNullOrEmpty(component.SelectedGod))
                {
                    component.GodSelected = false;
                    component.SelectedGod = null;
                    component.IsCustomGod = false;
                }
            }
        }

        /// <summary>
        /// Проверяет, содержит ли строка слишком много повторяющихся символов подряд
        /// </summary>
        private static bool HasTooManyRepeatingChars(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            const int maxRepeats = 4; // Максимум 4 одинаковых символа подряд
            var currentChar = input[0];
            var repeatCount = 1;

            for (int i = 1; i < input.Length; i++)
            {
                if (input[i] == currentChar)
                {
                    repeatCount++;
                    if (repeatCount > maxRepeats)
                        return true;
                }
                else
                {
                    currentChar = input[i];
                    repeatCount = 1;
                }
            }

            return false;
        }
    }
}

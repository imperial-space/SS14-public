using Content.Shared.Imperial.CustomChaplain;
using Content.Server.Popups;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Content.Shared.Bible;
using Robust.Shared.GameObjects;
using Content.Server.Bible.Components;

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
            _uiSystem.TryOpenUi(uid, GodSelectionUiKey.Key, user);
        }

        private void OnGodSelected(EntityUid uid, GodSelectionComponent component, GodSelectionChooseGodMessage message)
        {
            // Проверяем, был ли уже подтвержден выбор бога
            if (component.GodSelected && !string.IsNullOrEmpty(component.SelectedGod))
                return;

            component.SelectedGod = message.GodName;
            component.IsCustomGod = message.IsCustom;
            component.GodSelected = true;

            var godType = message.IsCustom ? "ваш собственный бог" : "известное божество";
            _popupSystem.PopupEntity($"Вы выбрали {message.GodName} ({godType})! Теперь вы будете служить ему.", uid);

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
    }
}

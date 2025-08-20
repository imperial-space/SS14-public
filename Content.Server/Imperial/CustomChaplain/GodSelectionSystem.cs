using Content.Shared.Imperial.CustomChaplain;
using Content.Server.Popups;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Content.Server.Bible.Components;
using System.Linq;

namespace Content.Server.Imperial.CustomChaplain;

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
            Text = Loc.GetString("god-selection-verb-label"),
            Priority = 1,
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
        var user = message.Actor;

        // Проверяем, был ли уже подтвержден выбор бога
        if (component.GodSelected && !string.IsNullOrEmpty(component.SelectedGod))
        {
            _popupSystem.PopupEntity(Loc.GetString("god-selection-already-selected"), uid, user);
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
            if (godName.Any(char.IsControl))
            {
                _popupSystem.PopupEntity(Loc.GetString("god-selection-server-invalid-characters"), uid, user);
                return;
            }

            // Проверка на недопустимые символы (HTML теги, специальные символы)
            if (godName.Any(ch => ch is '<' or '>' or '&' or '"' or '\'' or '\\' or '/'))
            {
                _popupSystem.PopupEntity(Loc.GetString("god-selection-server-invalid-characters"), uid, user);
                return;
            }

            // Проверка на слишком много повторяющихся символов (защита от спама)
            if (HasTooManyRepeatingChars(godName))
            {
                _popupSystem.PopupEntity(Loc.GetString("god-selection-server-invalid-characters"), uid, user);
                return;
            }
        }

        // Ограничиваем длину (даже после валидации, на всякий случай)
        if (godName.Length > 32)
            godName = godName[..32];

        component.SelectedGod = godName;
        component.IsCustomGod = message.IsCustom;
        component.GodSelected = true;

        _popupSystem.PopupEntity(Loc.GetString("god-selection-success", ("godName", godName)), uid, user);

        // Update UI state
        var state = new GodSelectionBuiState(component.GodSelected, component.SelectedGod, component.IsCustomGod);
        _uiSystem.SetUiState(uid, GodSelectionUiKey.Key, state);
    }

    private static void OnUIClosed(EntityUid uid, GodSelectionComponent component, BoundUIClosedEvent args)
    {
        // Если UI был закрыт без подтверждения выбора, полностью сбрасываем состояние
        if (!args.UiKey.Equals(GodSelectionUiKey.Key))
            return;

        // Сбрасываем состояние только если бог не был окончательно выбран
        if (component.GodSelected && !string.IsNullOrEmpty(component.SelectedGod))
            return;

        component.GodSelected = false;
        component.SelectedGod = null;
        component.IsCustomGod = false;
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

        for (var i = 1; i < input.Length; i++)
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

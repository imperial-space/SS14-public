using Content.Shared.Imperial.CustomChaplain;
using Content.Server.Popups;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Content.Server.Bible.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Imperial.CustomChaplain.Components;
using Content.Shared.UserInterface;
using Content.Shared.Store.Components;
using Content.Shared.Store;
using Robust.Shared.Prototypes;
using Robust.Shared.Localization;
using Robust.Shared.GameObjects;
using System.Linq;

using Content.Shared.Mind;

namespace Content.Server.Imperial.CustomChaplain;

public sealed class GodSelectionSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;

    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;

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
        // Проверяем, был ли уже подтвержден выбор бога в этой библии
        if (component.GodSelected && !string.IsNullOrEmpty(component.SelectedGod))
        {
            _popupSystem.PopupEntity(Loc.GetString("god-selection-already-selected"), uid, user);
            return;
        }

        // Проверяем, не выбрал ли игрок уже бога в другой библии
        if (HasPlayerAlreadySelectedGodElsewhere(user))
        {
            _popupSystem.PopupEntity(Loc.GetString("god-selection-god-already-selected-elsewhere"), uid, user);
            return;
        }

        var state = new GodSelectionBuiState(component.GodSelected, component.SelectedGod, component.IsCustomGod);
        _uiSystem.SetUiState(uid, GodSelectionUiKey.Key, state);
        _uiSystem.TryOpenUi(uid, GodSelectionUiKey.Key, user);
    }

    private void OnGodSelected(EntityUid uid, GodSelectionComponent component, GodSelectionChooseGodMessage message)
    {
        var user = message.Actor;

        // Проверяем, был ли уже подтвержден выбор бога в этой библии
        if (component.GodSelected && !string.IsNullOrEmpty(component.SelectedGod))
        {
            _popupSystem.PopupEntity(Loc.GetString("god-selection-already-selected"), uid, user);
            return;
        }

        // Проверяем, не выбрал ли игрок уже бога в другой библии
        if (HasPlayerAlreadySelectedGodElsewhere(user))
        {
            _popupSystem.PopupEntity(Loc.GetString("god-selection-god-already-selected-elsewhere"), uid, user);
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

        // Привязываем библию к пользователю и даём action возвращения
        BindBibleToUser(uid, user);

        _popupSystem.PopupEntity(Loc.GetString("god-selection-success", ("godName", godName)), uid, user);

        // Update UI state
        var state = new GodSelectionBuiState(component.GodSelected, component.SelectedGod, component.IsCustomGod);
        _uiSystem.SetUiState(uid, GodSelectionUiKey.Key, state);
    }

    private void BindBibleToUser(EntityUid bible, EntityUid user)
    {
        // Добавляем компонент библии, если его нет
        var bibleComp = EnsureComp<ImperialBibleComponent>(bible);
        bibleComp.Owner = user;
        bibleComp.IsBound = true;

        // Добавляем компонент магазина способностей к игроку
        var storeComp = EnsureComp<StoreComponent>(user);
        storeComp.Name = "Магазин способностей";
        storeComp.Balance[new ProtoId<CurrencyPrototype>("Faith")] = 0;
        storeComp.CurrencyWhitelist.Add(new ProtoId<CurrencyPrototype>("Faith"));
        storeComp.Categories.Add(new ProtoId<StoreCategoryPrototype>("CustomChaplainAbilities"));

        // Добавляем наш собственный компонент магазина
        var customStoreComp = EnsureComp<CustomChaplainStoreComponent>(user);
        customStoreComp.Name = "Магазин способностей";
        customStoreComp.FaithBalance = 0;
        customStoreComp.Categories = new List<ProtoId<StoreCategoryPrototype>> { new ProtoId<StoreCategoryPrototype>("CustomChaplainAbilities") };

        // Синхронизируем изменения с клиентом
        Dirty(user, storeComp);

        // Находим mind игрока и добавляем actions в контейнер mind
        if (_mind.TryGetMind(user, out var mindId, out _))
        {
            // Добавляем action возвращения библии
            var actionId = _actionContainer.AddAction(mindId, "ActionImperialBibleRecall");
            if (actionId != null)
            {
                bibleComp.RecallActionEntity = actionId.Value;
                Dirty(bible, bibleComp);
            }

            // Добавляем action магазина способностей
            var shopActionId = _actionContainer.AddAction(mindId, "ActionCustomChaplainShop");
            Log.Info($"Added shop action {shopActionId} to mind {mindId} for user {user}");
        }
        else
        {
            Log.Warning($"Could not find mind for user {user}");
        }
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

    /// <summary>
    /// Проверяет, не выбрал ли игрок уже бога в другой библии
    /// </summary>
    private bool HasPlayerAlreadySelectedGodElsewhere(EntityUid user)
    {
        // Ищем все библии с компонентом ImperialBibleComponent
        var query = EntityQueryEnumerator<ImperialBibleComponent>();

        while (query.MoveNext(out var bibleUid, out var bibleComp))
        {
            // Пропускаем текущую библию
            if (bibleUid == user)
                continue;

            // Если библия привязана к другому игроку, пропускаем
            if (bibleComp.Owner != user)
                continue;

            // Если библия привязана к текущему игроку, проверяем, есть ли у неё GodSelectionComponent
            if (TryComp<GodSelectionComponent>(bibleUid, out var godSelection))
            {
                // Если в этой библии уже выбран бог, то игрок не может выбрать в другой
                if (godSelection.GodSelected && !string.IsNullOrEmpty(godSelection.SelectedGod))
                {
                    return true;
                }
            }
        }

        return false;
    }
}

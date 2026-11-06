using System.Linq;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Changeling;
using Content.Shared.Mind.Components;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Changeling;

/// <summary>
/// tgui_alert и tgui_input_list для способностей: подтверждения стазиса и последнего шанса, выбор голоса.
/// </summary>
public sealed partial class ImperialChangelingSystem
{
    private readonly Dictionary<EntityUid, Action<int>> _pendingChoice = new();

    /// <summary>Подтверждённые способности: повторный запуск после ответа «Да».</summary>
    private readonly HashSet<(EntityUid, string)> _confirmed = new();

    private void InitializeChoice()
    {
        SubscribeLocalEvent<ImperialChangelingComponent, ImperialChangelingChoiceMessage>(OnChoice);
    }

    /// <summary>Открыть окно с вопросом; callback получит номер варианта.</summary>
    public void OpenChoice(EntityUid uid, string title, string message, List<string> options, Action<int> callback)
    {
        _pendingChoice[uid] = callback;
        var ui = EnsureComp<UserInterfaceComponent>(uid);
        if (!_ui.HasUi(uid, ImperialChangelingUiKey.Choice, ui))
            _ui.SetUi((uid, ui), ImperialChangelingUiKey.Choice, new InterfaceData("ImperialChangelingChoiceBoundUserInterface"));

        _ui.OpenUi(uid, ImperialChangelingUiKey.Choice, uid);
        _ui.SetUiState(uid, ImperialChangelingUiKey.Choice, new ImperialChangelingChoiceState(title, message, options));
    }

    private void OnChoice(Entity<ImperialChangelingComponent> ent, ref ImperialChangelingChoiceMessage args)
    {
        _ui.CloseUi(ent.Owner, ImperialChangelingUiKey.Choice);
        if (_pendingChoice.Remove(ent.Owner, out var callback) && args.Index >= 0)
            callback(args.Index);
    }

    /// <summary>tgui_alert(«…», list("Да", "Нет")): true, если уже подтверждено, иначе спрашивает и повторяет способность.</summary>
    private bool Confirm(Entity<ImperialChangelingComponent> ent, string power, string title, string message)
    {
        if (_confirmed.Remove((ent.Owner, power)))
            return true;

        OpenChoice(ent, Loc.GetString(title), Loc.GetString(message),
            new List<string> { Loc.GetString("imperial-changeling-choice-yes"), Loc.GetString("imperial-changeling-choice-no") },
            index =>
            {
                if (index != 0 || !TryComp<ImperialChangelingComponent>(ent, out var comp))
                    return;

                _confirmed.Add((ent.Owner, power));
                TryUsePower((ent.Owner, comp), power, null);
                _confirmed.Remove((ent.Owner, power));
            });
        return false;
    }

    /// <summary>mimicvoice: ручной ввод имени или существующий человек.</summary>
    private void ChooseMimicVoice(Entity<ImperialChangelingComponent> ent, ICommonSession session)
    {
        var options = new List<string>
        {
            Loc.GetString("imperial-changeling-mimic-manual"),
            Loc.GetString("imperial-changeling-mimic-existing"),
        };

        OpenChoice(ent, Loc.GetString("imperial-changeling-mimic-title"), Loc.GetString("imperial-changeling-mimic-choice"), options, index =>
        {
            if (index == 0)
            {
                _quickDialog.OpenDialog(session, Loc.GetString("imperial-changeling-mimic-title"), Loc.GetString("imperial-changeling-mimic-prompt"),
                    (string name) => ApplyMimic(ent, name));
                return;
            }

            // GLOB.human_to_tts: имена людей на станции.
            var names = new SortedSet<string>();
            var query = EntityQueryEnumerator<HumanoidProfileComponent, MindContainerComponent, MetaDataComponent>();
            while (query.MoveNext(out var uid, out _, out var mind, out var meta))
            {
                if (uid != ent.Owner && mind.HasMind)
                    names.Add(meta.EntityName);
            }

            var list = names.ToList();
            if (list.Count == 0)
                return;

            OpenChoice(ent, Loc.GetString("imperial-changeling-mimic-title"), Loc.GetString("imperial-changeling-mimic-pick"), list,
                pick => ApplyMimic(ent, list[pick]));
        });
    }

    private void ApplyMimic(EntityUid uid, string name)
    {
        name = name.Trim();
        if (name.Length == 0 || TerminatingOrDeleted(uid) || !TryComp<ImperialChangelingComponent>(uid, out var comp))
            return;

        if (name.Length > 32)
            name = name[..32];

        SetMimic((uid, comp), name);
    }

    /// <summary>Подсветка кнопки включённой способности (active).</summary>
    private void SetPowerToggled(Entity<ImperialChangelingComponent> ent, string power, bool toggled)
    {
        if (ent.Comp.Actions.TryGetValue(power, out var action))
            _actions.SetToggled(action, toggled);
    }
}

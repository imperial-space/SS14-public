using Content.Server.Chat.Managers;
using Content.Shared.Alert;
using Content.Shared.Imperial.Changeling;
using Content.Shared.Mind;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Changeling;

/// <summary>
/// Интерфейс TG: досье генокрада (AntagInfoChangeling), снятие жала щелчком по HUD, стрелка феромонов.
/// </summary>
public sealed partial class ImperialChangelingSystem
{
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;

    private static readonly EntProtoId InfoAction = "ActionImperialChangelingInfo";
    private static readonly ProtoId<AlertPrototype> PheromoneAlert = "ImperialChangelingPheromone";

    /// <summary>Порядок направлений в иконках алерта: S, N, E, W, SE, SW, NE, NW.</summary>
    private static readonly Direction[] AlertDirections =
    {
        Direction.South, Direction.North, Direction.East, Direction.West,
        Direction.SouthEast, Direction.SouthWest, Direction.NorthEast, Direction.NorthWest,
    };

    private void InitializeUi()
    {
        SubscribeLocalEvent<ImperialChangelingComponent, ImperialChangelingInfoActionEvent>(OnInfoAction);
        SubscribeLocalEvent<ImperialChangelingComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeNetworkEvent<ImperialChangelingUnsetStingEvent>(OnUnsetStingRequest);
    }

    /// <summary>on_gain(): кнопка «Открыть информацию о генокраде».</summary>
    private void GrantInfo(Entity<ImperialChangelingComponent> ent)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.InfoAction, InfoAction);
        var ui = EnsureComp<UserInterfaceComponent>(ent);
        _ui.SetUi((ent.Owner, ui), ImperialChangelingUiKey.Info, new InterfaceData("ImperialChangelingInfoBoundUserInterface"));
    }

    /// <summary>on_gain() → greet(): подсказка и само окно досье.</summary>
    private void Greet(Entity<ImperialChangelingComponent> ent)
    {
        if (TryComp<ActorComponent>(ent, out var actor))
        {
            // span_boldnotice
            var message = Loc.GetString("imperial-changeling-info-greet");
            var wrapped = $"[color=#6685f5][bold]{message}[/bold][/color]";
            _chat.ChatMessageToOne(Content.Shared.Chat.ChatChannel.Server, message, wrapped, default, false, actor.PlayerSession.Channel);
        }

        _ui.OpenUi(ent.Owner, ImperialChangelingUiKey.Info, ent.Owner);
    }

    private void OnInfoAction(Entity<ImperialChangelingComponent> ent, ref ImperialChangelingInfoActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _ui.TryToggleUi(ent.Owner, ImperialChangelingUiKey.Info, ent.Owner);
    }

    private void OnUiOpened(Entity<ImperialChangelingComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (Equals(args.UiKey, ImperialChangelingUiKey.Info))
            UpdateInfoUi(ent);
    }

    /// <summary>ui_data(): имя, улей, задачи, украденная память.</summary>
    public void UpdateInfoUi(Entity<ImperialChangelingComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, ImperialChangelingUiKey.Info))
            return;

        var objectives = new List<string>();
        if (_mind.TryGetMind(ent.Owner, out var mindId, out var mind))
        {
            foreach (var objective in mind.Objectives)
            {
                if (_objectives.GetInfo(objective, mindId, mind) is { } info)
                    objectives.Add(info.Title);
            }
        }

        _ui.SetUiState(ent.Owner, ImperialChangelingUiKey.Info, new ImperialChangelingInfoState(
            ent.Comp.ChangelingId,
            ent.Comp.HiveName,
            ent.Comp.AbsorbedCount,
            objectives,
            new List<ImperialChangelingMemory>(ent.Comp.Memories),
            ent.Comp.StolenAntagInfo));
    }

    /// <summary>/atom/movable/screen/ling/sting/Click() → unset_sting().</summary>
    private void OnUnsetStingRequest(ImperialChangelingUnsetStingEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user || !TryComp<ImperialChangelingComponent>(user, out var comp))
            return;

        if (comp.ChosenSting != null)
            UnsetSting((user, comp));
    }

    /// <summary>agent_pinpointer: pinonnull, pinondirect или стрелка close/medium/far по направлению.</summary>
    private void ShowPheromoneAlert(EntityUid uid, string state, Direction dir)
    {
        var index = Array.IndexOf(AlertDirections, dir);
        if (index < 0)
            index = 0;

        short severity = state switch
        {
            "pinonnull" => 0,
            "pinondirect" => 1,
            "pinonclose" => (short) (2 + index),
            "pinonmedium" => (short) (10 + index),
            _ => (short) (18 + index),
        };

        _alerts.ShowAlert(uid, PheromoneAlert, severity);
    }

    private void ClearPheromoneAlert(EntityUid uid)
    {
        _alerts.ClearAlert(uid, PheromoneAlert);
    }
}

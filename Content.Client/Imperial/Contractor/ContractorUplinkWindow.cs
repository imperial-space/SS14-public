using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.Contractor;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Contractor;

/// <summary>
/// Аплинк контрактника 1 в 1 с NTOS-программой SyndicateContractor из SS13 (тема syndicate, 500×600):
/// вход, «загрузка» в терминале, брифинг SyndTract v2.0, статус и контракты.
/// </summary>
public sealed class ContractorUplinkWindow : DefaultWindow
{
    public event Action? Login;
    public event Action? FirstLoadFinished;
    public event Action? ToggleInfo;
    public event Action? ClearError;
    public event Action<int>? AcceptContract;
    public event Action? AbortContract;
    public event Action? CallExtraction;
    public event Action? RedeemTc;

    private enum Screen
    {
        None,
        Login,
        FirstLoad,
        Info,
        Main,
    }

    private static readonly TguiTheme Syndie = TguiTheme.Syndicate;

    private static readonly string[] TerminalMessages =
    {
        "Запись биометрических данных...",
        "Анализ встроенной информации о синдикате...",
        "СТАТУС ПОДТВЕРЖДЕН",
        "Обращение к базе данных синдиката...",
        "Ожидание ответа...",
        "Ожидание ответа...",
        "Ожидание ответа...",
        "Ожидание ответа...",
        "Ожидание ответа...",
        "Ожидание ответа...",
        "Ответ получен, аккаунт 4851234...",
        "ПОДТВЕРДИТЬ АККАУНТ {0}",
        "Настройка личных аккаунтов...",
        "АККАУНТ КОНТРАКТНИКА СОЗДАН",
        "Поиск доступных контрактов...",
        "Поиск доступных контрактов...",
        "Поиск доступных контрактов...",
        "Поиск доступных контрактов...",
        "КОНТРАКТЫ НАЙДЕНЫ",
        "ДОБРО ПОЖАЛОВАТЬ, АГЕНТ",
    };

    private static readonly string[] InfoEntries =
    {
        "SyndTract v2.0",
        "",
        "Мы определили потенциально ценные цели, которые",
        "в настоящее время находятся в районе вашей миссии. Они могут",
        "хранить ценную информацию, которая может иметь важное",
        "значение для нашей организации.",
        "",
        "Ниже перечислены все доступные вам контракты. Вы",
        "должны довести заданную цель до назначенной",
        "зоны отправки, и связаться с нами через аплинк. Мы отправим",
        "специализированную капсулу для транспортировки, куда нужно поместить тело.",
        "",
        "Мы хотим, чтобы цели были живыми; мы платим меньшие",
        "суммы если цель будет мертва, так как вы просто не получите указанный",
        "бонус. Вы можете получить свою оплату через этот аплинк в",
        "форме телекристаллов, которые могут быть вложены в",
        "обычный аплинк Синдиката для приобретения необходимого вам снаряжения.",
        "Мы предоставим вам эти кристаллы в тот момент, когда вы отправите",
        "цель к нам, телекристаллы можно получить в любое время через",
        "эту систему.",
        "",
        "Похищенные цели будут выкуплены обратно на станцию после того,",
        "как их знания будут извлечены, и вам будет предоставлена",
        "часть выкупа. Вам следует помнить, что они могут",
        "идентифицировать вас, когда они вернутся. Мы предоставляем вам",
        "стандартное снаряжение контрактника, которое поможет скрыть вашу",
        "личность.",
    };

    private readonly BoxContainer _body = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true };
    private ContractorUplinkBoundUserInterfaceState? _state;
    private Screen _screen = Screen.None;

    // FakeTerminal.
    private BoxContainer? _terminal;
    private string[] _terminalLines = Array.Empty<string>();
    private int _terminalIndex;
    private float _terminalTimer;
    private float _terminalRate;
    private float _finishedTimer = -1;
    private bool _finishedSent;

    public ContractorUplinkWindow()
    {
        Title = Loc.GetString("contractor-uplink-window-title");
        MinSize = SetSize = new Vector2(500, 600);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true };
        root.AddChild(NtosHeader());
        root.AddChild(Tgui.Window(_body, Syndie, scrollable: false));
        Contents.AddChild(root);
    }

    /// <summary>Экран выбирается по ui_data сервера: logged_in, first_load, info_screen.</summary>
    public void UpdateState(ContractorUplinkBoundUserInterfaceState state)
    {
        _state = state;
        var screen = !state.LoggedIn ? Screen.Login
            : state.FirstLoad ? Screen.FirstLoad
            : state.InfoScreen ? Screen.Info
            : Screen.Main;

        // Терминал не перезапускается при каждом обновлении состояния.
        if (screen == _screen && screen is Screen.FirstLoad or Screen.Info)
            return;

        _screen = screen;
        Rebuild();
    }

    /// <summary>NtosHeader: «Syndix» слева, сеть и батарея справа.</summary>
    private static Control NtosHeader()
    {
        var header = Tgui.HBox(8,
            Tgui.Text("Syndix", Color.White, 12, true),
            new Control { HorizontalExpand = true },
            new SignalIcon(),
            new BatteryIcon(),
            Tgui.Text("100%", Color.FromHex("#9fd09f"), 11));
        header.Margin = new Thickness(8, 3);

        return new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = Syndie.Header },
            Children = { header },
        };
    }

    private void Rebuild()
    {
        _body.RemoveAllChildren();
        _terminal = null;
        _finishedTimer = -1;

        switch (_screen)
        {
            case Screen.None:
                break;
            case Screen.Login:
                BuildLogin();
                break;
            case Screen.FirstLoad:
                _finishedSent = false;
                var lines = (string[]) TerminalMessages.Clone();
                lines[11] = string.Format(lines[11], new Random().Next(20000));
                StartTerminal(lines, 2.5f);
                break;
            case Screen.Info:
                StartTerminal(InfoEntries, 10f);
                _body.AddChild(Tgui.Button(Loc.GetString("contractor-uplink-continue"), () => ToggleInfo?.Invoke(),
                    color: Tgui.Transparent, fluid: true, fontSize: 13, bold: true));
                break;
            default:
                BuildMain();
                break;
        }
    }

    #region Вход и терминал

    private void BuildLogin()
    {
        var login = Tgui.Button(Loc.GetString("contractor-uplink-login"), () => Login?.Invoke(),
            color: Tgui.Transparent, fontSize: 14, bold: true);
        login.HorizontalAlignment = HAlignment.Center;
        login.MinHeight = 40;

        var content = Tgui.VBox(8, login);
        if (_state is { Error.Length: > 0 } state)
            content.AddChild(Tgui.NoticeBox(state.Error, theme: Syndie));

        var section = Tgui.MakeSection(null, null, new Control
        {
            VerticalExpand = true,
            MinHeight = 500,
            Children = { content },
        }, Syndie);
        section.Margin = new Thickness(6);
        section.VerticalExpand = true;
        _body.AddChild(section);
    }

    /// <summary>FakeTerminal: строки появляются по одной на чёрном полупрозрачном фоне.</summary>
    private void StartTerminal(string[] lines, float linesPerSecond)
    {
        _terminalLines = lines;
        _terminalIndex = 0;
        _terminalTimer = 0;
        _terminalRate = linesPerSecond;

        _terminal = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(8) };
        _body.AddChild(new PanelContainer
        {
            VerticalExpand = true,
            Margin = new Thickness(6, 6, 6, 4),
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.Black.WithAlpha(0.8f) },
            Children =
            {
                new ScrollContainer
                {
                    HScrollEnabled = false,
                    Children = { _terminal },
                },
            },
        });
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_finishedTimer >= 0)
        {
            _finishedTimer -= args.DeltaSeconds;
            if (_finishedTimer < 0 && _screen == Screen.FirstLoad && !_finishedSent)
            {
                _finishedSent = true;
                FirstLoadFinished?.Invoke();
            }
            return;
        }

        if (_terminal == null || _terminalIndex >= _terminalLines.Length)
            return;

        _terminalTimer += args.DeltaSeconds;
        var interval = 1f / _terminalRate;
        while (_terminalTimer >= interval && _terminalIndex < _terminalLines.Length)
        {
            _terminalTimer -= interval;
            var line = _terminalLines[_terminalIndex++];
            _terminal.AddChild(new Label
            {
                Text = line.Length == 0 ? " " : line,
                FontOverride = Tgui.MonoFont(12),
                FontColorOverride = Color.White,
            });
        }

        // finishedTimeout = 3000 мс после загрузки.
        if (_terminalIndex >= _terminalLines.Length && _screen == Screen.FirstLoad)
            _finishedTimer = 3f;
    }

    #endregion

    #region Основной экран

    private void BuildMain()
    {
        if (_state is not { } state)
            return;

        var content = Tgui.VBox(8);
        content.Margin = new Thickness(6);
        content.AddChild(StatusPane(state));
        content.AddChild(ContractsTab(state));
        content.AddChild(DropoffLocator(state));

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, Children = { content } };
        if (string.IsNullOrEmpty(state.Error))
        {
            _body.AddChild(scroll);
            return;
        }

        // Modal поверх содержимого.
        _body.AddChild(new Control
        {
            VerticalExpand = true,
            Children =
            {
                scroll,
                new PanelContainer
                {
                    MouseFilter = MouseFilterMode.Stop,
                    PanelOverride = new StyleBoxFlat { BackgroundColor = Color.Black.WithAlpha(0.5f) },
                    Children = { ErrorModal(state.Error) },
                },
            },
        });
    }

    /// <summary>Modal backgroundColor="red": иконка, текст ошибки и «Dismiss».</summary>
    private Control ErrorModal(string error)
    {
        var text = Tgui.Paragraph(error);
        text.MinSize = new Vector2(260, 80);

        var dismiss = Tgui.Button(Loc.GetString("contractor-uplink-dismiss"), () => ClearError?.Invoke(), theme: Syndie);
        dismiss.HorizontalAlignment = HAlignment.Center;

        var row = Tgui.HBox(12, TguiIcons.Icon("exclamation-triangle", 48, Color.White), Tgui.VBox(6, text, dismiss));
        row.Margin = new Thickness(16);

        return new PanelContainer
        {
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#bd2020") },
            Children = { row },
        };
    }

    /// <summary>«Статус контрактника».</summary>
    private Control StatusPane(ContractorUplinkBoundUserInterfaceState state)
    {
        var infoButton = Tgui.Button(Loc.GetString("contractor-uplink-view-info"), () => ToggleInfo?.Invoke(),
            color: Tgui.Transparent, theme: Syndie);

        var redeem = Tgui.Button(Loc.GetString("contractor-uplink-redeem"), () => RedeemTc?.Invoke(),
            disabled: state.RedeemableTc <= 0, theme: Syndie);
        var redeemable = Tgui.HBox(6, Tgui.Text(state.RedeemableTc.ToString()), new Control { HorizontalExpand = true }, redeem);

        var left = Tgui.LabeledList(Syndie,
            (Loc.GetString("contractor-uplink-status-redeemable"), redeemable),
            (Loc.GetString("contractor-uplink-status-earned"), Tgui.Text(state.EarnedTc.ToString())));
        left.HorizontalExpand = true;

        var right = Tgui.LabeledList(Syndie,
            (Loc.GetString("contractor-uplink-status-completed"), Tgui.Text(state.ContractsCompleted.ToString())),
            (Loc.GetString("contractor-uplink-status-current"), Tgui.Text(Loc.GetString("contractor-uplink-status-active"))));
        right.HorizontalExpand = true;

        return Tgui.MakeSection(Loc.GetString("contractor-uplink-status-title"), infoButton, Tgui.HBox(12, left, right), Syndie);
    }

    /// <summary>«Доступные контракты»: при активном контракте показывается только он.</summary>
    private Control ContractsTab(ContractorUplinkBoundUserInterfaceState state)
    {
        var extraction = Tgui.Button(Loc.GetString("contractor-uplink-call-extraction"), () => CallExtraction?.Invoke(),
            disabled: !state.OngoingContract || state.ExtractionEnroute, theme: Syndie);

        var list = Tgui.VBox(8);
        foreach (var contract in state.Contracts)
        {
            if (state.OngoingContract && contract.Status != ContractorContractStatus.Active
                && contract.Status != ContractorContractStatus.Extracting)
                continue;
            if (contract.Status >= ContractorContractStatus.Complete)
                continue;

            list.AddChild(Contract(contract));
        }

        return Tgui.MakeSection(Loc.GetString("contractor-uplink-contracts-title"), extraction, list, Syndie);
    }

    private Control Contract(ContractorContractData contract)
    {
        var active = contract.Status > ContractorContractStatus.Inactive;
        var id = contract.Id;
        var button = Tgui.Button(Loc.GetString(active ? "contractor-uplink-abort" : "contractor-uplink-accept"),
            () =>
            {
                if (active)
                    AbortContract?.Invoke();
                else
                    AcceptContract?.Invoke(id);
            },
            color: active ? Tgui.Bad : null,
            disabled: contract.Status == ContractorContractStatus.Extracting,
            theme: Syndie);

        var buttons = Tgui.HBox(6,
            Tgui.Text(Loc.GetString("contractor-uplink-payout", ("payout", contract.Payout), ("bonus", contract.PayoutBonus)), bold: true),
            button);

        var message = Tgui.Paragraph(contract.Message);
        message.HorizontalExpand = true;
        var dropoff = Tgui.VBox(4,
            Tgui.Text(Loc.GetString("contractor-uplink-dropoff"), bold: true),
            Tgui.Text(contract.Dropoff));
        dropoff.MinWidth = 140;

        var title = string.IsNullOrEmpty(contract.Target)
            ? Loc.GetString("contractor-uplink-invalid-target")
            : $"{contract.Target} ({contract.TargetRank})";

        var section = Tgui.MakeSection(title, buttons, Tgui.HBox(10, message, dropoff), Syndie);
        if (section is PanelContainer panel)
            panel.PanelOverride = new StyleBoxFlat { BackgroundColor = Color.Black.WithAlpha(0.25f) };
        return section;
    }

    /// <summary>«Локатор зоны отправки»: виден только при активном контракте.</summary>
    private Control DropoffLocator(ContractorUplinkBoundUserInterfaceState state)
    {
        var section = Tgui.MakeSection(Loc.GetString("contractor-uplink-dropoff-locator"), null,
            Tgui.Text(state.DropoffDirection, bold: true, align: HAlignment.Center), Syndie);
        section.Modulate = state.OngoingContract ? Color.White : Color.Transparent;
        return section;
    }

    #endregion
}

/// <summary>Значок сети NTOS: четыре столбика нарастающей высоты.</summary>
public sealed class SignalIcon : Control
{
    private static readonly Color Bar = Color.FromHex("#9fd09f");

    public SignalIcon()
    {
        MinSize = new Vector2(16, 14);
        VerticalAlignment = VAlignment.Center;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var size = PixelSize;
        var scale = UIScale;
        var barWidth = 3 * scale;
        var gap = 1 * scale;
        for (var i = 0; i < 4; i++)
        {
            var height = size.Y * (i + 1) / 4f;
            var x = i * (barWidth + gap);
            handle.DrawRect(new UIBox2(x, size.Y - height, x + barWidth, size.Y), Bar);
        }
    }
}

/// <summary>Значок батареи NTOS: корпус с полной заливкой и контакт справа.</summary>
public sealed class BatteryIcon : Control
{
    private static readonly Color Body = Color.FromHex("#9fd09f");

    public BatteryIcon()
    {
        MinSize = new Vector2(22, 12);
        VerticalAlignment = VAlignment.Center;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var size = PixelSize;
        var scale = UIScale;
        var tip = 2 * scale;
        var border = 1 * scale;
        var body = new UIBox2(0, 0, size.X - tip, size.Y);

        handle.DrawRect(body, Body, filled: false);
        handle.DrawRect(new UIBox2(body.Left + border * 2, body.Top + border * 2, body.Right - border * 2, body.Bottom - border * 2), Body);
        handle.DrawRect(new UIBox2(size.X - tip, size.Y * 0.3f, size.X, size.Y * 0.7f), Body);
    }
}

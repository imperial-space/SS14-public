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
    private enum Screen
    {
        Login,
        FirstLoad,
        Info,
        Main,
    }

    public event Action<string, ContractorDifficulty>? AcceptContract;
    public event Action<string>? DeclineContract;
    public event Action<string>? BuyRequisition;
    public event Action? OpenPortal;

    /// <summary>Экран запоминается для каждого аплинка, как logged_in / first_load в SS13.</summary>
    private static readonly Dictionary<EntityUid, Screen> Screens = new();

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
        "должны довести заданную цель до назначенного маяка",
        "и связаться с нами через аплинк. Мы откроем",
        "портал для транспортировки, куда нужно поместить тело.",
        "",
        "Мы хотим, чтобы цели были живыми; мы платим меньшие",
        "суммы, если цель будет мертва. Выполненные контракты",
        "приносят телекристаллы и репутацию, на которую",
        "можно получить специальное снаряжение контрактника.",
        "",
        "Похищенные цели будут выкуплены обратно на станцию после того,",
        "как их знания будут извлечены. Вам следует помнить, что они могут",
        "идентифицировать вас, когда они вернутся. Мы предоставляем вам",
        "стандартное снаряжение контрактника, которое поможет скрыть вашу",
        "личность.",
    };

    private EntityUid _owner;
    private readonly BoxContainer _body = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true };
    private ContractorUplinkBoundUserInterfaceState? _state;

    // FakeTerminal.
    private BoxContainer? _terminal;
    private string[] _terminalLines = Array.Empty<string>();
    private int _terminalIndex;
    private float _terminalTimer;
    private float _terminalRate;
    private float _finishedTimer = -1;

    public ContractorUplinkWindow()
    {
        Title = Loc.GetString("contractor-uplink-window-title");
        MinSize = SetSize = new Vector2(500, 600);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true };
        root.AddChild(NtosHeader());
        root.AddChild(Tgui.Window(_body, Syndie, scrollable: false));
        Contents.AddChild(root);
    }

    /// <summary>Привязать окно к аплинку: экран входа и загрузки показывается один раз на аплинк.</summary>
    public void SetOwner(EntityUid owner)
    {
        _owner = owner;
        Screens.TryAdd(owner, Screen.Login);
        Rebuild();
    }

    private Screen Current
    {
        get => Screens.GetValueOrDefault(_owner, Screen.Login);
        set
        {
            Screens[_owner] = value;
            Rebuild();
        }
    }

    public void UpdateState(ContractorUplinkBoundUserInterfaceState state)
    {
        _state = state;
        if (Current == Screen.Main)
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

        switch (Current)
        {
            case Screen.Login:
                BuildLogin();
                break;
            case Screen.FirstLoad:
                var lines = (string[]) TerminalMessages.Clone();
                lines[11] = string.Format(lines[11], new Random().Next(20000));
                StartTerminal(lines, 2.5f);
                break;
            case Screen.Info:
                StartTerminal(InfoEntries, 10f);
                var proceed = Tgui.Button(Loc.GetString("contractor-uplink-continue"), () => Current = Screen.Main,
                    color: Tgui.Transparent, fluid: true, fontSize: 13, bold: true);
                _body.AddChild(proceed);
                break;
            default:
                BuildMain();
                break;
        }
    }

    #region Вход и терминал

    private void BuildLogin()
    {
        var login = Tgui.Button(Loc.GetString("contractor-uplink-login"), () => Current = Screen.FirstLoad,
            color: Tgui.Transparent, fontSize: 14, bold: true);
        login.HorizontalAlignment = HAlignment.Center;
        login.VerticalAlignment = VAlignment.Center;
        login.MinHeight = 40;

        var section = Tgui.MakeSection(null, null, new Control
        {
            VerticalExpand = true,
            MinHeight = 500,
            Children = { login },
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
            if (_finishedTimer < 0 && Current == Screen.FirstLoad)
                Current = Screen.Info;
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
        if (_terminalIndex >= _terminalLines.Length && Current == Screen.FirstLoad)
            _finishedTimer = 3f;
    }

    #endregion

    #region Основной экран

    private void BuildMain()
    {
        var content = Tgui.VBox(8);
        content.Margin = new Thickness(6);
        _body.AddChild(new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, Children = { content } });

        if (_state is not { } state)
        {
            content.AddChild(Tgui.NoticeBox(Loc.GetString("contractor-uplink-connecting"), theme: Syndie));
            return;
        }

        content.AddChild(StatusPane(state));
        content.AddChild(ContractsTab(state));
        if (state.HasActiveContract)
            content.AddChild(DropoffLocator(state));
        content.AddChild(Requisitions(state));
    }

    /// <summary>«Статус контрактника».</summary>
    private Control StatusPane(ContractorUplinkBoundUserInterfaceState state)
    {
        var infoButton = Tgui.Button(Loc.GetString("contractor-uplink-view-info"), () => Current = Screen.Info,
            color: Tgui.Transparent, theme: Syndie);

        var left = Tgui.LabeledList(Syndie,
            (Loc.GetString("contractor-uplink-status-reputation"), Tgui.Text(state.Reputation.ToString(), bold: true)));
        left.HorizontalExpand = true;

        var right = Tgui.LabeledList(Syndie,
            (Loc.GetString("contractor-uplink-status-current"), Tgui.Text(Loc.GetString("contractor-uplink-status-active"), Tgui.Good, bold: true)));
        right.HorizontalExpand = true;

        var content = Tgui.VBox(6, Tgui.HBox(12, left, right));
        if (!string.IsNullOrWhiteSpace(state.StatusText))
            content.AddChild(Tgui.Paragraph(state.StatusText, Syndie.Label));

        return Tgui.MakeSection(Loc.GetString("contractor-uplink-status-title"), infoButton, content, Syndie);
    }

    /// <summary>«Доступные контракты»: активный контракт либо предложения.</summary>
    private Control ContractsTab(ContractorUplinkBoundUserInterfaceState state)
    {
        var extraction = Tgui.Button(Loc.GetString("contractor-uplink-dispense-falsefire"), () => OpenPortal?.Invoke(),
            disabled: !state.CanOpenPortal, theme: Syndie);

        var list = Tgui.VBox(8);
        if (state.HasActiveContract)
        {
            list.AddChild(ActiveContract(state));
        }
        else if (state.Offers.Length == 0)
        {
            list.AddChild(Tgui.Text(Loc.GetString("contractor-uplink-no-offers"), Syndie.Label, italic: true));
        }
        else
        {
            foreach (var offer in state.Offers)
            {
                list.AddChild(Offer(offer));
            }
        }

        return Tgui.MakeSection(Loc.GetString("contractor-uplink-contracts-title"), extraction, list, Syndie);
    }

    private static string DifficultyName(ContractorDifficulty difficulty)
    {
        return Loc.GetString($"contractor-difficulty-{difficulty.ToString().ToLowerInvariant()}");
    }

    private static Color DifficultyColor(ContractorDifficulty difficulty)
    {
        return difficulty switch
        {
            ContractorDifficulty.Easy => Tgui.Good,
            ContractorDifficulty.Medium => Tgui.Average,
            _ => Tgui.Bad,
        };
    }

    /// <summary>Выплата как в SS13: «payout (+payout_bonus) ТК», бонус — надбавка за живую цель.</summary>
    private static string Payout(Content.Shared.FixedPoint.FixedPoint2 alive, Content.Shared.FixedPoint.FixedPoint2 dead)
    {
        return Loc.GetString("contractor-uplink-payout-short", ("payout", dead), ("bonus", alive - dead));
    }

    /// <summary>Вложенная секция контракта: «Цель (должность)», выплата и кнопка справа.</summary>
    private Control ContractSection(string title, Control buttons, Control content)
    {
        var section = Tgui.MakeSection(title, buttons, content, Syndie);
        if (section is PanelContainer panel)
            panel.PanelOverride = new StyleBoxFlat { BackgroundColor = Color.Black.WithAlpha(0.25f) };
        return section;
    }

    private Control ActiveContract(ContractorUplinkBoundUserInterfaceState state)
    {
        var active = state.ActiveContract;
        var buttons = Tgui.HBox(6,
            Tgui.Text(Payout(active.AlivePayout, active.DeadPayout), bold: true),
            Tgui.Text(DifficultyName(active.Difficulty), DifficultyColor(active.Difficulty), bold: true));

        var stage = state.ActiveStage switch
        {
            ContractorContractStage.PortalReady => Loc.GetString("contractor-uplink-stage-portal"),
            ContractorContractStage.AwaitingBeacon => Loc.GetString("contractor-uplink-stage-beacon"),
            _ => null,
        };

        var message = Tgui.VBox(4, Tgui.Paragraph(active.Reason));
        if (stage != null)
            message.AddChild(Tgui.Text(stage, Tgui.Average, bold: true));

        message.HorizontalExpand = true;
        var dropoff = Tgui.VBox(2,
            Tgui.Text(Loc.GetString("contractor-uplink-dropoff"), bold: true),
            Tgui.Text(active.BeaconLabel));
        dropoff.MinWidth = 150;

        return ContractSection($"{active.TargetName} ({active.Job})", buttons, Tgui.HBox(10, message, dropoff));
    }

    private Control Offer(ContractorContractOfferData offer)
    {
        var decline = Tgui.Button(Loc.GetString("contractor-uplink-decline"), () => DeclineContract?.Invoke(offer.Id),
            color: Tgui.Bad, theme: Syndie);

        var options = Tgui.VBox(8);
        foreach (var option in offer.Options)
        {
            var id = offer.Id;
            var difficulty = option.Difficulty;
            var accept = Tgui.Button(Loc.GetString("contractor-uplink-accept"), () => AcceptContract?.Invoke(id, difficulty),
                theme: Syndie);
            accept.MinWidth = 80;

            var header = Tgui.HBox(8,
                Tgui.Text(DifficultyName(option.Difficulty).ToUpperInvariant(), DifficultyColor(option.Difficulty), bold: true),
                Tgui.Text(Payout(option.AlivePayout, option.DeadPayout), bold: true),
                new Control { HorizontalExpand = true },
                accept);

            var reason = Tgui.Paragraph(option.Reason);
            var dropoff = Tgui.VBox(2,
                Tgui.Text(Loc.GetString("contractor-uplink-dropoff"), bold: true),
                Tgui.Text(option.BeaconLabel));
            dropoff.MinWidth = 150;

            options.AddChild(new PanelContainer
            {
                PanelOverride = new StyleBoxFlat
                {
                    BackgroundColor = Color.Black.WithAlpha(0.2f),
                    BorderColor = DifficultyColor(option.Difficulty).WithAlpha(0.6f),
                    BorderThickness = new Thickness(2, 0, 0, 0),
                },
                Children =
                {
                    new BoxContainer
                    {
                        Orientation = BoxContainer.LayoutOrientation.Vertical,
                        SeparationOverride = 4,
                        Margin = new Thickness(8, 5),
                        Children = { header, Tgui.HBox(10, reason, dropoff) },
                    },
                },
            });
        }

        return ContractSection($"{offer.TargetName} ({offer.Job})", decline, options);
    }

    /// <summary>«Локатор зоны отправки».</summary>
    private Control DropoffLocator(ContractorUplinkBoundUserInterfaceState state)
    {
        return Tgui.MakeSection(Loc.GetString("contractor-uplink-dropoff-locator"), null,
            Tgui.Text(state.ActiveContract.BeaconLabel, bold: true, size: 14, align: HAlignment.Center), Syndie);
    }

    /// <summary>Снаряжение контрактника за репутацию (хаб контрактника).</summary>
    private Control Requisitions(ContractorUplinkBoundUserInterfaceState state)
    {
        var list = Tgui.VBox(6);
        foreach (var requisition in state.Requisitions)
        {
            var id = requisition.Id;
            var buy = Tgui.Button(Loc.GetString("contractor-uplink-buy-short", ("cost", requisition.Cost)),
                () => BuyRequisition?.Invoke(id), disabled: !requisition.Affordable, bold: true, theme: Syndie);
            buy.MinWidth = 90;

            var info = Tgui.VBox(2,
                Tgui.Text(requisition.Name, bold: true),
                Tgui.Paragraph(requisition.Description, Syndie.Label));
            info.HorizontalExpand = true;

            list.AddChild(new PanelContainer
            {
                PanelOverride = new StyleBoxFlat { BackgroundColor = Color.Black.WithAlpha(0.2f) },
                Children = { new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    SeparationOverride = 8,
                    Margin = new Thickness(8, 5),
                    Children = { info, buy },
                } },
            });
        }

        return Tgui.MakeSection(Loc.GetString("contractor-uplink-requisitions-title"), null, list, Syndie);
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

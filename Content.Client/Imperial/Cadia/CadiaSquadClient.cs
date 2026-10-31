using System.Linq;
using System.Numerics;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Cadia;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Cadia;

public sealed class CadiaSquadBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CadiaSquadWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CadiaSquadWindow>();
        _window.Title = Loc.GetString("cadia-squad-ui-title");
        _window.OnSend += SendMessage;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is CadiaSquadState squads)
            _window?.Update(squads);
    }
}

/// <summary>
/// Окно командира: слева — отряды (приказы, вокс-команды, роспуск), справа — бойцы стороны с выбором отряда.
/// </summary>
public sealed class CadiaSquadWindow : DefaultWindow
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public event Action<BoundUserInterfaceMessage>? OnSend;

    private readonly BoxContainer _squads = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };
    private readonly BoxContainer _fighters = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
    private readonly LineEdit _newName = new() { HorizontalExpand = true };
    private readonly Label _vox = new();
    private CadiaSquadState? _state;
    private string _key = string.Empty;

    public CadiaSquadWindow()
    {
        IoCManager.InjectDependencies(this);
        MinSize = SetSize = new Vector2(1100, 680);

        _newName.PlaceHolder = Loc.GetString("cadia-squad-ui-new-placeholder");
        var create = new Button { Text = Loc.GetString("cadia-squad-ui-create") };
        create.OnPressed += _ =>
        {
            OnSend?.Invoke(new CadiaSquadCreateMessage(_newName.Text));
            _newName.Text = string.Empty;
        };

        var left = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, MinWidth = 560, SeparationOverride = 6 };
        left.AddChild(new Label { Text = Loc.GetString("cadia-squad-ui-squads"), FontColorOverride = Color.FromHex("#e8c66a") });
        var createRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        createRow.AddChild(_newName);
        createRow.AddChild(create);
        left.AddChild(createRow);
        left.AddChild(_vox);
        var squadScroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        squadScroll.AddChild(_squads);
        left.AddChild(squadScroll);

        var right = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 6, Margin = new Thickness(10, 0, 0, 0) };
        right.AddChild(new Label { Text = Loc.GetString("cadia-squad-ui-fighters"), FontColorOverride = Color.FromHex("#e8c66a") });
        var fighterScroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        fighterScroll.AddChild(_fighters);
        right.AddChild(fighterScroll);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(6) };
        root.AddChild(left);
        root.AddChild(right);
        Contents.AddChild(root);
    }

    public void Update(CadiaSquadState state)
    {
        _state = state;
        // Перестраиваем списки, только когда что-то поменялось, — иначе нажатия теряются.
        var key = string.Join(";", state.Squads.Select(s => $"{s.Number}:{s.Name}:{s.Leader}:{s.Order}:{string.Join(",", s.Members)}")) + "|" +
                  string.Join(";", state.Fighters.Select(f => $"{f.Entity}:{f.Squad}:{f.Alive}"));
        if (key == _key)
            return;
        _key = key;
        BuildSquads(state);
        BuildFighters(state);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_state == null)
            return;
        var left = _state.VoxCooldownUntil - _timing.CurTime;
        _vox.Text = left > TimeSpan.Zero
            ? Loc.GetString("cadia-squad-ui-vox-cooldown", ("seconds", (int) Math.Ceiling(left.TotalSeconds)))
            : Loc.GetString("cadia-squad-ui-vox-ready");
        _vox.FontColorOverride = left > TimeSpan.Zero ? Color.FromHex("#ff9a3c") : Color.FromHex("#6dff8a");
    }

    private void BuildSquads(CadiaSquadState state)
    {
        _squads.RemoveAllChildren();
        if (state.Squads.Count == 0)
            _squads.AddChild(new Label { Text = Loc.GetString("cadia-squad-ui-none"), FontColorOverride = Color.FromHex("#9a9a8a") });

        foreach (var squad in state.Squads)
        {
            var panel = new PanelContainer
            {
                PanelOverride = new StyleBoxFlat
                {
                    BackgroundColor = squad.Color.WithAlpha(0.08f),
                    BorderColor = squad.Color,
                    BorderThickness = new Thickness(2, 1, 1, 1),
                    ContentMarginLeftOverride = 8,
                    ContentMarginTopOverride = 4,
                    ContentMarginBottomOverride = 4,
                },
            };
            var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };

            var leaderName = squad.Leader is { } leader ? state.Fighters.FirstOrDefault(f => f.Entity == leader).Name : null;
            var title = $"{squad.Number}. {squad.Name}  —  {Loc.GetString("cadia-squad-ui-members", ("count", squad.Members.Count))}";
            box.AddChild(new Label { Text = title, FontColorOverride = squad.Color });
            box.AddChild(new Label
            {
                Text = Loc.GetString("cadia-squad-ui-leader", ("name", leaderName ?? Loc.GetString("cadia-squad-ui-no-leader"))) + "   " +
                       Loc.GetString("cadia-squad-ui-order", ("order", Loc.GetString($"cadia-squad-order-{squad.Order.ToString().ToLowerInvariant()}"))),
                FontColorOverride = Color.FromHex("#b8c8b8"),
            });

            var orders = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
            foreach (var order in new[] { CadiaSquadOrder.Hold, CadiaSquadOrder.Advance, CadiaSquadOrder.Retreat })
            {
                var number = squad.Number;
                var o = order;
                var button = new Button { Text = Loc.GetString($"cadia-squad-order-{order.ToString().ToLowerInvariant()}"), ToggleMode = true, Pressed = squad.Order == order };
                button.OnPressed += _ => OnSend?.Invoke(new CadiaSquadOrderMessage(number, o));
                orders.AddChild(button);
            }
            var disband = new Button { Text = Loc.GetString("cadia-squad-ui-disband") };
            disband.StyleClasses.Add("Caution");
            var n = squad.Number;
            disband.OnPressed += _ => OnSend?.Invoke(new CadiaSquadDisbandMessage(n));
            orders.AddChild(new Control { HorizontalExpand = true });
            orders.AddChild(disband);
            box.AddChild(orders);

            var vox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
            vox.AddChild(new Label { Text = Loc.GetString("cadia-squad-ui-vox"), FontColorOverride = Color.FromHex("#9a9a8a") });
            foreach (var command in Enum.GetValues<CadiaVoxCommand>())
            {
                var number = squad.Number;
                var c = command;
                var button = new Button { Text = Loc.GetString($"cadia-squad-vox-{command.ToString().ToLowerInvariant()}"), ToolTip = Loc.GetString($"cadia-squad-vox-{command.ToString().ToLowerInvariant()}-desc") };
                button.OnPressed += _ => OnSend?.Invoke(new CadiaSquadVoxMessage(number, c));
                vox.AddChild(button);
            }
            box.AddChild(vox);
            panel.AddChild(box);
            _squads.AddChild(panel);
        }
    }

    private void BuildFighters(CadiaSquadState state)
    {
        _fighters.RemoveAllChildren();
        foreach (var fighter in state.Fighters)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
            var squad = state.Squads.FirstOrDefault(s => s.Number == fighter.Squad);
            var color = !fighter.Alive ? Color.FromHex("#6a6a6a") : squad?.Color ?? Color.FromHex("#d8e0c8");
            var distance = fighter.Distance >= 0 ? $" · {(int) fighter.Distance} м" : string.Empty;
            row.AddChild(new Label
            {
                Text = $"{fighter.Name} ({fighter.Role}){distance}{(fighter.Alive ? string.Empty : " ✝")}",
                FontColorOverride = color,
                HorizontalExpand = true,
                ClipText = true,
            });

            // Кнопки отрядов: «—» вне отряда, номера — перевести в отряд.
            var entity = fighter.Entity;
            var none = new Button { Text = "—", ToggleMode = true, Pressed = fighter.Squad == 0, MinWidth = 26 };
            none.OnPressed += _ => OnSend?.Invoke(new CadiaSquadAssignMessage(entity, 0));
            row.AddChild(none);
            foreach (var s in state.Squads)
            {
                var number = s.Number;
                var button = new Button { Text = number.ToString(), ToggleMode = true, Pressed = fighter.Squad == number, MinWidth = 26 };
                button.ModulateSelfOverride = s.Color;
                button.OnPressed += _ => OnSend?.Invoke(new CadiaSquadAssignMessage(entity, number));
                row.AddChild(button);
            }
            if (fighter.Squad != 0)
            {
                var isLeader = squad?.Leader == fighter.Entity;
                var lead = new Button { Text = "★", ToggleMode = true, Pressed = isLeader, ToolTip = Loc.GetString("cadia-squad-ui-make-leader"), MinWidth = 26 };
                lead.OnPressed += _ => OnSend?.Invoke(new CadiaSquadLeaderMessage(entity));
                row.AddChild(lead);
            }
            _fighters.AddChild(row);
        }
    }
}

/// <summary>
/// Знак отряда над головой: цветной номер (у командира отряда — со звездой) и значок приказа.
/// Видят только бойцы той же стороны и призраки.
/// </summary>
public sealed class CadiaSquadOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEyeManager _eye = default!;

    private readonly Font _font;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CadiaSquadOverlay()
    {
        IoCManager.InjectDependencies(this);
        var cache = IoCManager.Resolve<IResourceCache>();
        _font = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 10);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local)
            return;
        var ghost = _entMan.HasComponent<GhostComponent>(local);
        CadiaFaction? side = _entMan.TryGetComponent<CadiaFactionMemberComponent>(local, out var me) && me.Role != null ? me.Faction : null;
        if (!ghost && side == null)
            return;

        var handle = args.ScreenHandle;
        var xforms = _entMan.System<SharedTransformSystem>();
        var mapId = args.MapId;
        var query = _entMan.EntityQueryEnumerator<CadiaSquadMemberComponent, TransformComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var squad, out var xform, out var sprite))
        {
            if (xform.MapID != mapId || !sprite.Visible || (!ghost && squad.Faction != side))
                continue;
            var world = xforms.GetWorldPosition(xform) + new Vector2(0.45f, 0.75f);
            var screen = _eye.WorldToScreen(world);
            if (!args.ViewportBounds.Contains(new Vector2i((int) screen.X, (int) screen.Y)))
                continue;

            var text = squad.Leader ? $"★{squad.Number}" : squad.Number.ToString();
            var size = handle.GetDimensions(_font, text, 1f);
            var box = UIBox2.FromDimensions(screen - new Vector2(3, 1), size + new Vector2(6, 2));
            handle.DrawRect(box, Color.Black.WithAlpha(0.6f));
            handle.DrawRect(box, squad.Color, false);
            handle.DrawString(_font, screen, text, squad.Color);

            // Значок приказа справа от номера.
            var o = new Vector2(box.Right + 6, box.Center.Y);
            switch (squad.Order)
            {
                case CadiaSquadOrder.Hold:
                    handle.DrawRect(UIBox2.FromDimensions(o - new Vector2(3), new Vector2(6)), squad.Color, false);
                    break;
                case CadiaSquadOrder.Advance:
                    handle.DrawLine(o + new Vector2(-3, 3), o + new Vector2(0, -3), squad.Color);
                    handle.DrawLine(o + new Vector2(0, -3), o + new Vector2(3, 3), squad.Color);
                    break;
                case CadiaSquadOrder.Retreat:
                    handle.DrawLine(o + new Vector2(-3, -3), o + new Vector2(0, 3), squad.Color);
                    handle.DrawLine(o + new Vector2(0, 3), o + new Vector2(3, -3), squad.Color);
                    break;
            }
        }
    }
}

public sealed class CadiaSquadOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;

    private CadiaSquadOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new CadiaSquadOverlay();
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_overlay != null)
            _overlays.RemoveOverlay(_overlay);
    }
}

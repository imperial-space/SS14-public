using System.Numerics;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Cadia;

/// <summary>Всё, что игрок видит вне боя: памятка роли, вступление, экран ожидания, шторм, транспорт и знамёна секторов.</summary>
public sealed class CadiaCampaignClientSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private const string ShipLayer = "ship";
    private const string ShadowLayer = "shadow";
    private const string SectorLayer = "banner";
    private const float ShipHeight = 14f;

    private CadiaWaitingOverlay? _waiting;
    private CadiaStormOverlay? _storm;
    private CadiaObjectiveOverlay? _objective;
    private CadiaWeatherOverlay? _weather;
    private CadiaEndingControl? _ending;
    private CadiaFeedOverlay? _feed;
    private CadiaBriefingWindow? _briefing;
    private CadiaIntroControl? _intro;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CadiaBriefingEvent>(OnBriefing);
        SubscribeNetworkEvent<CadiaIntroEvent>(OnIntro);
        SubscribeNetworkEvent<CadiaEndingEvent>(OnEnding);
        SubscribeNetworkEvent<CadiaFeedEvent>(ev => _feed?.Add(ev.Text, ev.Color));
        SubscribeLocalEvent<CadiaSpawnProtectionComponent, ComponentShutdown>(OnProtectionEnded);
        SubscribeLocalEvent<CadiaSectorComponent, AfterAutoHandleStateEvent>(OnSectorState);
        SubscribeLocalEvent<CadiaSectorComponent, ComponentStartup>(OnSectorStartup);

        _waiting = new CadiaWaitingOverlay();
        _storm = new CadiaStormOverlay();
        _weather = new CadiaWeatherOverlay();
        _overlays.AddOverlay(_weather);
        _objective = new CadiaObjectiveOverlay();
        _feed = new CadiaFeedOverlay();
        _overlays.AddOverlay(_waiting);
        _overlays.AddOverlay(_storm);
        _overlays.AddOverlay(_objective);
        _overlays.AddOverlay(_feed);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_waiting != null)
            _overlays.RemoveOverlay(_waiting);
        if (_storm != null)
            _overlays.RemoveOverlay(_storm);
        if (_weather != null)
            _overlays.RemoveOverlay(_weather);
        if (_objective != null)
            _overlays.RemoveOverlay(_objective);
        if (_feed != null)
            _overlays.RemoveOverlay(_feed);
        _briefing?.Close();
        _intro?.Orphan();
        _ending?.Orphan();
    }

    private void OnBriefing(CadiaBriefingEvent ev)
    {
        _briefing?.Close();
        _briefing = new CadiaBriefingWindow(ev.Role, ev.Name);
        _briefing.OnClose += () => _briefing = null;
        _briefing.OpenCentered();
    }

    private void OnIntro(CadiaIntroEvent ev)
    {
        _intro?.Orphan();
        _intro = new CadiaIntroControl();
        _intro.Finished += () => _intro = null;
        LayoutContainer.SetAnchorPreset(_intro, LayoutContainer.LayoutPreset.Wide);
        _ui.WindowRoot.AddChild(_intro);
    }

    private void OnProtectionEnded(Entity<CadiaSpawnProtectionComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetColor((ent, sprite), Color.White);
    }

    private void OnEnding(CadiaEndingEvent ev)
    {
        _intro?.Orphan();
        _ending?.Orphan();
        _ending = new CadiaEndingControl(ev.Ending, ev.Saved);
        _ending.Finished += () => _ending = null;
        LayoutContainer.SetAnchorPreset(_ending, LayoutContainer.LayoutPreset.Wide);
        _ui.WindowRoot.AddChild(_ending);
    }

    private void OnSectorStartup(Entity<CadiaSectorComponent> ent, ref ComponentStartup args) => UpdateBanner(ent);

    private void OnSectorState(Entity<CadiaSectorComponent> ent, ref AfterAutoHandleStateEvent args) => UpdateBanner(ent);

    private void UpdateBanner(Entity<CadiaSectorComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;
        var state = ent.Comp.Owner switch
        {
            CadiaFaction.Imperium => "imperium",
            CadiaFaction.Chaos => "chaos",
            _ => "neutral",
        };
        _sprite.LayerSetRsiState((ent, sprite), SectorLayer, state);
    }

    /// <summary>Транспорт снижается с неба, стоит на площадке и уходит вверх. Сервер только считает время.</summary>
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var now = _timing.CurTime;

        // Неуязвимый новичок мерцает, чтобы враг не тратил на него патроны.
        var shimmer = 0.55f + 0.35f * MathF.Sin((float) now.TotalSeconds * 8f);
        var shielded = EntityQueryEnumerator<CadiaSpawnProtectionComponent, SpriteComponent>();
        while (shielded.MoveNext(out var protectedUid, out _, out var protectedSprite))
            _sprite.SetColor((protectedUid, protectedSprite), Color.White.WithAlpha(shimmer));

        var query = EntityQueryEnumerator<CadiaEvacShipComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var ship, out var sprite))
        {
            float air;
            if (now < ship.LandAt)
            {
                var total = (float) (ship.LandAt - ship.Start).TotalSeconds;
                air = total > 0f ? 1f - Math.Clamp((float) (now - ship.Start).TotalSeconds / total, 0f, 1f) : 0f;
                air *= air;
            }
            else if (now < ship.LeaveAt)
            {
                air = 0f;
            }
            else
            {
                var total = (float) (ship.GoneAt - ship.LeaveAt).TotalSeconds;
                air = total > 0f ? Math.Clamp((float) (now - ship.LeaveAt).TotalSeconds / total, 0f, 1f) : 1f;
                air *= air;
            }

            var hover = MathF.Sin((float) now.TotalSeconds * 3f) * 0.06f;
            var ent = new Entity<SpriteComponent?>(uid, sprite);
            _sprite.LayerSetOffset(ent, ShipLayer, new Vector2(0f, air * ShipHeight + 0.35f + hover));
            _sprite.LayerSetScale(ent, ShadowLayer, new Vector2(6f - air * 4f));
            _sprite.LayerSetColor(ent, ShadowLayer, Color.White.WithAlpha(0.8f - air * 0.6f));
        }
    }
}

/// <summary>Памятка роли: кто ты, зачем ты здесь и что нажимать. Открывается при появлении и действием на панели.</summary>
public sealed class CadiaBriefingWindow : DefaultWindow
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public CadiaBriefingWindow(string roleId, string name)
    {
        IoCManager.InjectDependencies(this);
        MinSize = SetSize = new Vector2(620, 620);
        Title = Loc.GetString("cadia-guide-title");

        if (!_proto.TryIndex<CadiaRolePrototype>(roleId, out var role))
            return;

        var accent = role.Faction == CadiaFaction.Imperium ? "#e8c66a" : "#d23b2a";
        var markup = new FormattedMessage();
        void Section(string header, string body)
        {
            markup.AddMarkupPermissive($"[color={accent}][bold]{header}[/bold][/color]\n{body}\n\n");
        }

        markup.AddMarkupPermissive($"[font size=16][color={accent}][bold]{FormattedMessage.EscapeText(name)}[/bold][/color][/font]\n");
        markup.AddMarkupPermissive($"{Loc.GetString(role.Name)}\n\n");
        Section(Loc.GetString("cadia-guide-task"), role.Briefing is { } briefing ? Loc.GetString(briefing) : Loc.GetString(role.Description));
        Section(Loc.GetString("cadia-guide-goal"), Loc.GetString(role.Faction == CadiaFaction.Imperium ? "cadia-goal-imperium" : "cadia-goal-chaos"));
        foreach (var tip in role.Tips)
            Section(Loc.GetString($"{tip}-title"), Loc.GetString(tip));
        Section(Loc.GetString("cadia-tip-common-title"), Loc.GetString("cadia-tip-common"));

        var text = new RichTextLabel { HorizontalExpand = true, Margin = new Thickness(8) };
        text.SetMessage(markup);
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(text);

        var close = new Button { Text = Loc.GetString("cadia-guide-close"), HorizontalAlignment = HAlignment.Center, MinWidth = 180, Margin = new Thickness(0, 6) };
        close.OnPressed += _ => Close();

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        root.AddChild(scroll);
        root.AddChild(close);
        Contents.AddChild(root);
    }
}

/// <summary>Вступительная сцена: три картины с медленным наездом и текстом, кнопка пропуска. Мир за ней живёт дальше.</summary>
public sealed class CadiaIntroControl : Control
{
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public event Action? Finished;

    private const int Cards = 3;
    private const float CardSeconds = 9f;
    private const float FadeSeconds = 1.2f;
    private const float Zoom = 0.08f;
    private const float TextMargin = 60f;

    private readonly Texture[] _images = new Texture[Cards];
    private readonly string[] _texts = new string[Cards];
    private readonly Font _font;
    private readonly TimeSpan _start;
    private bool _done;

    public CadiaIntroControl()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Stop;
        _start = _timing.RealTime;
        _font = new VectorFont(_cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 20);
        for (var i = 0; i < Cards; i++)
        {
            _images[i] = _cache.GetResource<TextureResource>($"/Textures/Imperial/Cadia/War/Intro/card{i + 1}.png").Texture;
            _texts[i] = Loc.GetString($"cadia-intro-{i + 1}");
        }

        var skip = new Button
        {
            Text = Loc.GetString("cadia-intro-skip"),
            HorizontalAlignment = HAlignment.Right,
            VerticalAlignment = VAlignment.Bottom,
            Margin = new Thickness(0, 0, 24, 24),
            MinWidth = 140,
        };
        skip.OnPressed += _ => Finish();
        AddChild(skip);
    }

    private void Finish()
    {
        if (_done)
            return;
        _done = true;
        // Убираемся после обхода интерфейса: Finish зовётся из FrameUpdate, пока родитель перебирает детей.
        Visible = false;
        Robust.Shared.Timing.Timer.Spawn(0, () =>
        {
            Finished?.Invoke();
            Orphan();
        });
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if ((float) (_timing.RealTime - _start).TotalSeconds >= CardSeconds * Cards)
            Finish();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var size = PixelSize;
        handle.DrawRect(new UIBox2(0, 0, size.X, size.Y), Color.Black);

        var elapsed = (float) (_timing.RealTime - _start).TotalSeconds;
        var card = Math.Clamp((int) (elapsed / CardSeconds), 0, Cards - 1);
        var local = elapsed - card * CardSeconds;
        var alpha = Math.Clamp(Math.Min(local, CardSeconds - local) / FadeSeconds, 0f, 1f);

        // Картина вписана целиком по большей стороне и медленно приближается.
        var image = _images[card];
        var scale = Math.Max((float) size.X / image.Width, (float) size.Y / image.Height) * (1f + Zoom * local / CardSeconds);
        var drawn = new Vector2(image.Width, image.Height) * scale;
        var at = (new Vector2(size.X, size.Y) - drawn) / 2f;
        handle.DrawTextureRect(image, UIBox2.FromDimensions(at, drawn), Color.White.WithAlpha(alpha));

        // Плашка снизу, чтобы текст читался на любой картине.
        var text = _texts[card];
        var lines = text.Split('\n');
        var lineHeight = _font.GetLineHeight(1f);
        var blockHeight = lines.Length * lineHeight + 28f;
        var top = size.Y - blockHeight - TextMargin;
        handle.DrawRect(new UIBox2(0, top, size.X, top + blockHeight), Color.Black.WithAlpha(0.7f * alpha));
        for (var i = 0; i < lines.Length; i++)
        {
            var width = handle.GetDimensions(_font, lines[i], 1f).X;
            handle.DrawString(_font, new Vector2((size.X - width) / 2f, top + 14f + i * lineHeight), lines[i], Color.FromHex("#e8c66a").WithAlpha(alpha));
        }
    }
}

/// <summary>Финальная сцена: большая картина исхода с медленным наездом, заголовок и две строки рассказа. Идёт до окна итогов.</summary>
public sealed class CadiaEndingControl : Control
{
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public event Action? Finished;

    private const float TotalSeconds = 24f;
    private const float FadeSeconds = 1.6f;
    private const float TitleAt = 1.2f;
    private const float SecondTextAt = 12.5f;
    private const float Zoom = 0.1f;

    private readonly Texture _image;
    private readonly string _title;
    private readonly string _first;
    private readonly string _second;
    private readonly Color _accent;
    private readonly Font _titleFont;
    private readonly Font _font;
    private readonly TimeSpan _start;
    private bool _done;

    public CadiaEndingControl(CadiaEnding ending, int saved)
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Stop;
        _start = _timing.RealTime;
        _titleFont = new VectorFont(_cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 34);
        _font = new VectorFont(_cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 19);

        var key = ending.ToString().ToLowerInvariant();
        _image = _cache.GetResource<TextureResource>($"/Textures/Imperial/Cadia/War/Intro/ending_{key}.png").Texture;
        _title = Loc.GetString($"cadia-ending-{key}-title");
        _first = Loc.GetString($"cadia-ending-{key}-1", ("saved", saved));
        _second = Loc.GetString($"cadia-ending-{key}-2", ("saved", saved));
        _accent = ending is CadiaEnding.Held or CadiaEnding.Banished or CadiaEnding.Retreat
            ? Color.FromHex("#e8c66a")
            : Color.FromHex("#ff5a3c");

        var skip = new Button
        {
            Text = Loc.GetString("cadia-intro-skip"),
            HorizontalAlignment = HAlignment.Right,
            VerticalAlignment = VAlignment.Bottom,
            Margin = new Thickness(0, 0, 24, 24),
            MinWidth = 140,
        };
        skip.OnPressed += _ => Finish();
        AddChild(skip);
    }

    private void Finish()
    {
        if (_done)
            return;
        _done = true;
        // Убираемся после обхода интерфейса: Finish зовётся из FrameUpdate, пока родитель перебирает детей.
        Visible = false;
        Robust.Shared.Timing.Timer.Spawn(0, () =>
        {
            Finished?.Invoke();
            Orphan();
        });
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if ((float) (_timing.RealTime - _start).TotalSeconds >= TotalSeconds)
            Finish();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var size = PixelSize;
        handle.DrawRect(new UIBox2(0, 0, size.X, size.Y), Color.Black);

        var elapsed = (float) (_timing.RealTime - _start).TotalSeconds;
        var scene = Math.Clamp(Math.Min(elapsed, TotalSeconds - elapsed) / FadeSeconds, 0f, 1f);
        var scale = Math.Max((float) size.X / _image.Width, (float) size.Y / _image.Height) * (1f + Zoom * elapsed / TotalSeconds);
        var drawn = new Vector2(_image.Width, _image.Height) * scale;
        handle.DrawTextureRect(_image, UIBox2.FromDimensions((new Vector2(size.X, size.Y) - drawn) / 2f, drawn), Color.White.WithAlpha(scene));

        // Заголовок сверху, рассказ снизу: так обе надписи не закрывают главное на картине.
        var title = Math.Clamp((elapsed - TitleAt) / FadeSeconds, 0f, 1f) * scene;
        handle.DrawRect(new UIBox2(0, 0, size.X, 96f), Color.Black.WithAlpha(0.6f * title));
        var titleWidth = handle.GetDimensions(_titleFont, _title, 1f).X;
        handle.DrawString(_titleFont, new Vector2((size.X - titleWidth) / 2f, 24f), _title, _accent.WithAlpha(title));

        var second = elapsed >= SecondTextAt;
        var local = second ? elapsed - SecondTextAt : elapsed;
        var length = second ? TotalSeconds - SecondTextAt : SecondTextAt;
        var alpha = Math.Clamp(Math.Min(local, length - local) / 1f, 0f, 1f);
        var lines = (second ? _second : _first).Split('\n');
        var lineHeight = _font.GetLineHeight(1f);
        var blockHeight = lines.Length * lineHeight + 26f;
        var top = size.Y - blockHeight - 56f;
        handle.DrawRect(new UIBox2(0, top, size.X, top + blockHeight), Color.Black.WithAlpha(0.7f * alpha));
        for (var i = 0; i < lines.Length; i++)
        {
            var width = handle.GetDimensions(_font, lines[i], 1f).X;
            handle.DrawString(_font, new Vector2((size.X - width) / 2f, top + 13f + i * lineHeight), lines[i], Color.White.WithAlpha(alpha));
        }
    }
}

/// <summary>
/// Погода под открытым небом. Туман и гарь закрывают края экрана, пепел ещё и падает хлопьями, которые держатся за мир, а не за экран.
/// </summary>
public sealed class CadiaWeatherOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IEyeManager _eye = default!;

    private const float RampSeconds = 8f;
    private const float MaskScale = 1.25f;
    private const int Flakes = 140;
    private const float PixelsPerTile = 32f;

    private readonly Texture _mask;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CadiaWeatherOverlay()
    {
        IoCManager.InjectDependencies(this);
        _mask = IoCManager.Resolve<IResourceCache>().GetResource<TextureResource>("/Textures/Imperial/Cadia/War/Intro/fog_mask.png").Texture;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        CadiaWarStateComponent? war = null;
        var query = _entMan.EntityQueryEnumerator<CadiaWarStateComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            war = comp;
            break;
        }
        if (war == null || war.Weather == CadiaWeather.Clear || _player.LocalEntity is not { } local)
            return;
        if (_entMan.GetComponent<TransformComponent>(local).MapUid is not { } map || _entMan.HasComponent<CadiaVehicleInteriorComponent>(map) ||
            !_entMan.TryGetComponent<CadiaFlightLevelComponent>(map, out var level) || level.Level != CadiaFlightLevel.Surface)
            return;

        var now = _timing.CurTime;
        var ramp = Math.Clamp(Math.Min((float) (now - war.WeatherStart).TotalSeconds, (float) (war.WeatherEnd - now).TotalSeconds) / RampSeconds, 0f, 1f);
        if (ramp <= 0f)
            return;

        var (tint, density) = war.Weather switch
        {
            CadiaWeather.Fog => (Color.FromHex("#b4b8ae"), 0.94f),
            CadiaWeather.Ash => (Color.FromHex("#6a645c"), 0.5f),
            _ => (Color.FromHex("#2e2a24"), 0.82f),
        };
        var handle = args.ScreenHandle;
        var bounds = args.ViewportBounds;
        var side = Math.Max(bounds.Width, bounds.Height) * MaskScale;
        var center = new Vector2(bounds.Left + bounds.Width / 2f, bounds.Top + bounds.Height / 2f);
        var rect = UIBox2.FromDimensions(center - new Vector2(side / 2f), new Vector2(side));
        handle.DrawTextureRect(_mask, rect, tint.WithAlpha(density * ramp));

        // Квадрат маски шире экрана, поэтому на вытянутых мониторах края тоже закрыты.
        if (war.Weather != CadiaWeather.Ash)
            return;
        var eye = _eye.CurrentEye.Position.Position * PixelsPerTile;
        var time = (float) now.TotalSeconds;
        for (var i = 0; i < Flakes; i++)
        {
            var seedX = (i * 7919 % 1000) / 1000f;
            var seedY = (i * 104729 % 1000) / 1000f;
            var speed = 16f + (i * 13 % 10) * 2f;
            var sway = MathF.Sin(time * 0.7f + i) * 10f;
            var x = Wrap(seedX * bounds.Width + sway - time * 6f - eye.X, bounds.Width);
            var y = Wrap(seedY * bounds.Height + time * speed + eye.Y, bounds.Height);
            var dot = 1.5f + (i % 3) * 0.75f;
            var color = (i % 5 == 0 ? Color.FromHex("#e8843a") : Color.FromHex("#c8c4bc")).WithAlpha(0.75f * ramp);
            handle.DrawRect(UIBox2.FromDimensions(new Vector2(bounds.Left + x, bounds.Top + y), new Vector2(dot)), color);
        }
    }

    private static float Wrap(float value, float size)
    {
        var result = value % size;
        return result < 0f ? result + size : result;
    }
}

/// <summary>Выбывший боец видит, когда подкрепления и есть ли места, и почему их может не быть.</summary>
public sealed class CadiaWaitingOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly Color Gold = Color.FromHex("#e8c66a");
    private static readonly Color Blood = Color.FromHex("#d23b2a");
    private static readonly Color Back = Color.FromHex("#0b0d0ae0");
    private const float Width = 470f;
    private const float Height = 76f;

    private readonly Font _title;
    private readonly Font _font;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CadiaWaitingOverlay()
    {
        IoCManager.InjectDependencies(this);
        var cache = IoCManager.Resolve<IResourceCache>();
        _title = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 12);
        _font = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 11);
    }

    private bool IsOutOfBattle()
    {
        if (_player.LocalEntity is not { } local)
            return false;
        if (_entMan.HasComponent<GhostComponent>(local))
            return true;
        return _entMan.TryGetComponent<CadiaFactionMemberComponent>(local, out var member) && member.Role != null &&
               _entMan.TryGetComponent<MobStateComponent>(local, out var state) && state.CurrentState == MobState.Dead;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!IsOutOfBattle())
            return;
        CadiaWarStateComponent? war = null;
        var query = _entMan.EntityQueryEnumerator<CadiaWarStateComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            war = comp;
            break;
        }
        if (war == null || war.Phase == CadiaWarPhase.Ended)
            return;

        var handle = args.ScreenHandle;
        var origin = new Vector2(args.ViewportBounds.Left + (args.ViewportBounds.Width - Width) / 2f, args.ViewportBounds.Top + 126f);
        var box = UIBox2.FromDimensions(origin, new Vector2(Width, Height));
        handle.DrawRect(box, Back);
        handle.DrawRect(box, Gold.WithAlpha(0.6f), false);
        handle.DrawString(_title, origin + new Vector2(10, 5), Loc.GetString("cadia-wait-title"), Gold);
        handle.DrawString(_font, origin + new Vector2(10, 24), Line(war, CadiaFaction.Imperium), Gold);
        handle.DrawString(_font, origin + new Vector2(10, 40), Line(war, CadiaFaction.Chaos), Blood);
        handle.DrawString(_font, origin + new Vector2(10, 56), Loc.GetString("cadia-wait-hint"), Color.White.WithAlpha(0.8f));
    }

    private string Line(CadiaWarStateComponent war, CadiaFaction side)
    {
        var name = Loc.GetString(side == CadiaFaction.Imperium ? "cadia-wait-imperium" : "cadia-wait-chaos");
        if (side == CadiaFaction.Imperium && war.Phase == CadiaWarPhase.Evacuation)
            return Loc.GetString("cadia-wait-evac", ("side", name));
        if (side == CadiaFaction.Imperium ? war.ImperiumClosed : war.ChaosClosed)
            return Loc.GetString("cadia-wait-closed", ("side", name));

        var seats = side == CadiaFaction.Imperium ? war.ImperiumSeats : war.ChaosSeats;
        var left = (side == CadiaFaction.Imperium ? war.NextImperiumWave : war.NextChaosWave) - _timing.CurTime;
        if (left < TimeSpan.Zero)
            left = TimeSpan.Zero;
        return Loc.GetString("cadia-wait-seats", ("side", name), ("seats", seats), ("time", $"{(int) left.TotalMinutes}:{left.Seconds:00}"));
    }
}

/// <summary>Варп-шторм под открытым небом: красный ливень, тёмные края при шёпоте, вспышки при прорыве демонов.</summary>
public sealed class CadiaStormOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const int Drops = 160;
    private const float DropLength = 16f;
    private const float RampSeconds = 3f;
    private const int VignetteSteps = 8;
    private const float VignetteDepth = 150f;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CadiaStormOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        CadiaWarStateComponent? war = null;
        var query = _entMan.EntityQueryEnumerator<CadiaWarStateComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            war = comp;
            break;
        }
        if (war == null || war.Storm == CadiaWarpStorm.None || _player.LocalEntity is not { } local)
            return;
        var map = _entMan.GetComponent<TransformComponent>(local).MapUid;
        if (map is not { } mapUid || _entMan.HasComponent<CadiaVehicleInteriorComponent>(mapUid) ||
            _entMan.TryGetComponent<CadiaFlightLevelComponent>(mapUid, out var level) && level.Level == CadiaFlightLevel.Orbit)
            return;

        var now = _timing.CurTime;
        var left = (float) (war.StormEnd - now).TotalSeconds;
        if (left <= 0f)
            return;
        var ramp = Math.Clamp(left / RampSeconds, 0f, 1f);
        var time = (float) now.TotalSeconds;
        var handle = args.ScreenHandle;
        var bounds = args.ViewportBounds;

        switch (war.Storm)
        {
            case CadiaWarpStorm.BloodRain:
                handle.DrawRect(bounds, Color.FromHex("#5a0a12").WithAlpha(0.22f * ramp));
                for (var i = 0; i < Drops; i++)
                {
                    // Псевдослучайно, но одинаково из кадра в кадр: иначе капли будут дрожать, а не падать.
                    var seedX = (i * 7919 % 1000) / 1000f;
                    var seedY = (i * 104729 % 1000) / 1000f;
                    var speed = 0.55f + (i * 31 % 10) * 0.04f;
                    var fall = (seedY + time * speed) % 1f;
                    var x = bounds.Left + seedX * bounds.Width - fall * 60f;
                    var y = bounds.Top + fall * bounds.Height;
                    handle.DrawLine(new Vector2(x, y), new Vector2(x - 4f, y + DropLength), Color.FromHex("#b0121f").WithAlpha(0.55f * ramp));
                }
                break;
            case CadiaWarpStorm.Whispers:
                var pulse = 0.55f + 0.3f * MathF.Sin(time * 1.3f);
                for (var i = 0; i < VignetteSteps; i++)
                {
                    var inset = VignetteDepth * i / VignetteSteps;
                    var band = VignetteDepth / VignetteSteps;
                    var alpha = pulse * ramp * (1f - (float) i / VignetteSteps) * 0.5f;
                    var color = Color.FromHex("#14051e").WithAlpha(alpha);
                    handle.DrawRect(new UIBox2(bounds.Left, bounds.Top + inset, bounds.Right, bounds.Top + inset + band), color);
                    handle.DrawRect(new UIBox2(bounds.Left, bounds.Bottom - inset - band, bounds.Right, bounds.Bottom - inset), color);
                    handle.DrawRect(new UIBox2(bounds.Left + inset, bounds.Top, bounds.Left + inset + band, bounds.Bottom), color);
                    handle.DrawRect(new UIBox2(bounds.Right - inset - band, bounds.Top, bounds.Right - inset, bounds.Bottom), color);
                }
                break;
            case CadiaWarpStorm.DaemonSurge:
                var flash = MathF.Max(0f, MathF.Sin(time * 5f) * MathF.Sin(time * 1.7f));
                handle.DrawRect(bounds, Color.FromHex("#7a1fb0").WithAlpha(0.28f * flash * ramp));
                break;
        }
    }
}

/// <summary>
/// Цель бойца: приказ отряда, а без него ближайшее важное по фазе и стороне. Строка под панелью войны и стрелка у центра экрана.
/// </summary>
public sealed class CadiaObjectiveOverlay : Overlay
{
    private readonly Vector2[] _arrow = new Vector2[3];

    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEyeManager _eye = default!;

    private static readonly Color Gold = Color.FromHex("#e8c66a");
    private static readonly Color Blood = Color.FromHex("#ff5a3c");
    private static readonly Color Back = Color.FromHex("#0b0d0ae0");
    private const float Width = 470f;
    private const float Top = 125f;
    private const float RowHeight = 19f;
    private const float ArrowRadius = 130f;
    private const float ArrowSize = 11f;
    private const float NearEnough = 7f;

    private readonly Font _font;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CadiaObjectiveOverlay()
    {
        IoCManager.InjectDependencies(this);
        _font = new VectorFont(IoCManager.Resolve<IResourceCache>().GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 11);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local ||
            !_entMan.TryGetComponent<CadiaFactionMemberComponent>(local, out var member) || member.Role == null ||
            !_entMan.TryGetComponent<MobStateComponent>(local, out var state) || state.CurrentState == MobState.Dead)
            return;

        CadiaWarStateComponent? war = null;
        var query = _entMan.EntityQueryEnumerator<CadiaWarStateComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            war = comp;
            break;
        }
        if (war == null || war.Phase == CadiaWarPhase.Ended)
            return;

        var xforms = _entMan.System<SharedTransformSystem>();
        var xform = _entMan.GetComponent<TransformComponent>(local);
        var here = xforms.GetWorldPosition(xform);
        var (text, target) = Pick(local, member.Faction, war, here, (int) xform.MapID, xform.MapUid);

        var handle = args.ScreenHandle;
        var color = member.Faction == CadiaFaction.Imperium ? Gold : Blood;
        var bounds = args.ViewportBounds;
        var origin = new Vector2(bounds.Left + (bounds.Width - Width) / 2f, bounds.Top + Top);
        var box = UIBox2.FromDimensions(origin, new Vector2(Width, RowHeight));
        handle.DrawRect(box, Back);
        handle.DrawRect(box, color.WithAlpha(0.5f), false);

        var line = Loc.GetString("cadia-objective-line", ("text", text));
        if (target is { } point)
        {
            var delta = point - here;
            line = Loc.GetString("cadia-objective-line-distance", ("text", text), ("meters", (int) delta.Length()));

            var screen = _eye.WorldToScreen(point) - _eye.WorldToScreen(here);
            if (delta.Length() > NearEnough && screen.LengthSquared() > 1f)
            {
                var dir = Vector2.Normalize(screen);
                var side = new Vector2(-dir.Y, dir.X);
                var center = new Vector2(bounds.Left + bounds.Width / 2f, bounds.Top + bounds.Height / 2f) + dir * ArrowRadius;
                var tip = center + dir * ArrowSize;
                var left = center - dir * ArrowSize * 0.6f + side * ArrowSize * 0.8f;
                var right = center - dir * ArrowSize * 0.6f - side * ArrowSize * 0.8f;
                // stackalloc запрещён песочницей клиента.
                _arrow[0] = tip;
                _arrow[1] = left;
                _arrow[2] = right;
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _arrow, color.WithAlpha(0.75f));
            }
        }
        handle.DrawString(_font, origin + new Vector2(8, 2), line, color);
    }

    private (string Text, Vector2? Target) Pick(EntityUid local, CadiaFaction side, CadiaWarStateComponent war, Vector2 here, int map, EntityUid? mapUid)
    {
        // Приказ командира важнее любой подсказки.
        if (_entMan.TryGetComponent<CadiaSquadMemberComponent>(local, out var squad) && squad.Order != CadiaSquadOrder.None)
        {
            var sameLevel = mapUid is { } m && _entMan.TryGetComponent<CadiaFlightLevelComponent>(m, out var level) && level.Level == squad.OrderLevel;
            return (Loc.GetString($"cadia-objective-order-{squad.Order.ToString().ToLowerInvariant()}"), sameLevel ? squad.OrderPoint : null);
        }

        if (war.Phase == CadiaWarPhase.Evacuation)
        {
            var beacon = Nearest(war, CadiaLandmarkKind.Beacon, here, map);
            return (Loc.GetString(side == CadiaFaction.Imperium ? "cadia-objective-evac-imperium" : "cadia-objective-evac-chaos"), beacon?.Position);
        }

        // Флот на орбите: точек на его карте нет, он работает по приказам с голокарты.
        var pylon = Nearest(war, CadiaLandmarkKind.Pylon, here, map);
        if (pylon == null)
            return (Loc.GetString("cadia-objective-orders"), null);

        if (war.Phase < CadiaWarPhase.Assault)
        {
            return side == CadiaFaction.Imperium
                ? (Loc.GetString("cadia-objective-prepare", ("number", CadiaObjectiveNames.Roman(pylon.Value.Number))), pylon.Value.Position)
                : (Loc.GetString("cadia-objective-staging"), null);
        }

        // Спорный сектор рядом важнее пилона: там решается, куда высадится следующая волна.
        CadiaSectorInfo? sector = null;
        var sectorDistance = float.MaxValue;
        foreach (var info in war.Sectors)
        {
            if (info.Map != map)
                continue;
            var wanted = info.Contested || info.Owner != side;
            if (!wanted)
                continue;
            var distance = (info.Position - here).LengthSquared();
            if (distance < sectorDistance)
            {
                sector = info;
                sectorDistance = distance;
            }
        }
        var pylonDistance = (pylon.Value.Position - here).LengthSquared();
        if (sector is { } taken && sectorDistance < pylonDistance)
            return (Loc.GetString(taken.Owner == side ? "cadia-objective-hold-sector" : "cadia-objective-take-sector", ("letter", taken.Letter)), taken.Position);

        var number = CadiaObjectiveNames.Roman(pylon.Value.Number);
        return (Loc.GetString(side == CadiaFaction.Imperium ? "cadia-objective-defend-pylon" : "cadia-objective-attack-pylon", ("number", number)), pylon.Value.Position);
    }

    private static CadiaLandmark? Nearest(CadiaWarStateComponent war, CadiaLandmarkKind kind, Vector2 here, int map)
    {
        CadiaLandmark? best = null;
        var bestDistance = float.MaxValue;
        foreach (var point in war.Landmarks)
        {
            if (point.Kind != kind || point.Map != map)
                continue;
            var distance = (point.Position - here).LengthSquared();
            if (distance < bestDistance)
            {
                best = point;
                bestDistance = distance;
            }
        }
        return best;
    }
}

public static class CadiaObjectiveNames
{
    public static string Roman(int number)
    {
        if (number <= 0)
            return "?";
        var result = string.Empty;
        foreach (var (value, numeral) in new (int, string)[] { (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I") })
        {
            while (number >= value)
            {
                result += numeral;
                number -= value;
            }
        }
        return result;
    }
}

/// <summary>Лента событий слева: короткие строки о главном в бою, старые гаснут сами.</summary>
public sealed class CadiaFeedOverlay : Overlay
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private const int MaxLines = 6;
    private const float LifeSeconds = 14f;
    private const float FadeSeconds = 2f;
    private const float RowHeight = 20f;
    private const float MaxWidth = 380f;

    private readonly Font _font;
    private readonly List<(string Text, Color Color, TimeSpan At)> _lines = new();

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CadiaFeedOverlay()
    {
        IoCManager.InjectDependencies(this);
        _font = new VectorFont(IoCManager.Resolve<IResourceCache>().GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 11);
    }

    public void Add(string text, Color color)
    {
        _lines.Add((text, color, _timing.RealTime));
        if (_lines.Count > MaxLines)
            _lines.RemoveAt(0);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var now = _timing.RealTime;
        _lines.RemoveAll(l => (now - l.At).TotalSeconds >= LifeSeconds);
        if (_lines.Count == 0)
            return;

        var handle = args.ScreenHandle;
        var x = args.ViewportBounds.Left + 12f;
        var y = args.ViewportBounds.Top + args.ViewportBounds.Height * 0.32f;
        foreach (var (text, color, at) in _lines)
        {
            var left = LifeSeconds - (float) (now - at).TotalSeconds;
            var alpha = Math.Clamp(left / FadeSeconds, 0f, 1f);
            var width = Math.Min(handle.GetDimensions(_font, text, 1f).X + 14f, MaxWidth);
            handle.DrawRect(UIBox2.FromDimensions(new Vector2(x, y), new Vector2(width, RowHeight - 2f)), Color.FromHex("#0b0d0a").WithAlpha(0.75f * alpha));
            handle.DrawRect(UIBox2.FromDimensions(new Vector2(x, y), new Vector2(3f, RowHeight - 2f)), color.WithAlpha(alpha));
            handle.DrawString(_font, new Vector2(x + 8f, y + 1f), text, Color.White.WithAlpha(alpha));
            y += RowHeight;
        }
    }
}

/// <summary>Окно ведущего. Каждая кнопка только просит сервер, а тот сам проверяет права.</summary>
public sealed class CadiaEventPanelWindow : DefaultWindow
{
    [Dependency] private readonly IEntityManager _entMan = default!;

    private readonly Label _status = new();
    private readonly Button _veilToggle = new() { HorizontalExpand = true };
    private readonly LineEdit _announce = new() { HorizontalExpand = true };
    private readonly LineEdit _filter = new() { HorizontalExpand = true };
    private readonly BoxContainer _players = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
    private readonly Button _closeImperium = new();
    private readonly Button _closeChaos = new();
    private CadiaPanelStateEvent? _state;
    private string _shown = string.Empty;

    public CadiaEventPanelWindow()
    {
        IoCManager.InjectDependencies(this);
        MinSize = SetSize = new Vector2(900, 720);
        Title = Loc.GetString("cadia-panel-title");

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(8), SeparationOverride = 6 };
        root.AddChild(_status);

        root.AddChild(Row("cadia-panel-war",
            ActionButton("cadia-panel-start", CadiaPanelAction.Start),
            ActionButton("cadia-panel-next-phase", CadiaPanelAction.NextPhase),
            ActionButton("cadia-panel-intro", CadiaPanelAction.ShowIntro),
            ActionButton("cadia-panel-fall", CadiaPanelAction.BeginFall)));
        root.AddChild(Row("cadia-panel-waves",
            ActionButton("cadia-panel-wave-imperium", CadiaPanelAction.WaveImperium),
            ActionButton("cadia-panel-wave-chaos", CadiaPanelAction.WaveChaos),
            _closeImperium,
            _closeChaos));
        _closeImperium.OnPressed += _ => Send(CadiaPanelAction.ToggleCloseImperium);
        _closeChaos.OnPressed += _ => Send(CadiaPanelAction.ToggleCloseChaos);
        root.AddChild(Row("cadia-panel-storms",
            ActionButton("cadia-panel-storm-rain", CadiaPanelAction.StormBloodRain),
            ActionButton("cadia-panel-storm-whispers", CadiaPanelAction.StormWhispers),
            ActionButton("cadia-panel-storm-surge", CadiaPanelAction.StormDaemonSurge),
            ActionButton("cadia-panel-warp", CadiaPanelAction.AddWarp),
            ActionButton("cadia-panel-weather", CadiaPanelAction.CycleWeather)));
        root.AddChild(Row("cadia-panel-veil",
            _veilToggle,
            ActionButton("cadia-panel-veil-add", CadiaPanelAction.VeilAddMinute),
            ActionButton("cadia-panel-veil-cut", CadiaPanelAction.VeilCutMinute),
            ActionButton("cadia-panel-drop-recharge", CadiaPanelAction.DropRecharge),
            ActionButton("cadia-panel-drop-pod", CadiaPanelAction.DropPod)));
        _veilToggle.OnPressed += _ => Send(CadiaPanelAction.VeilToggle);
        root.AddChild(Row("cadia-panel-win",
            ActionButton("cadia-panel-win-imperium", CadiaPanelAction.WinImperium),
            ActionButton("cadia-panel-win-chaos", CadiaPanelAction.WinChaos)));

        _announce.PlaceHolder = Loc.GetString("cadia-panel-announce-placeholder");
        var announce = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        announce.AddChild(_announce);
        announce.AddChild(AnnounceButton("cadia-panel-announce-all", CadiaPanelAction.AnnounceAll));
        announce.AddChild(AnnounceButton("cadia-panel-announce-imperium", CadiaPanelAction.AnnounceImperium));
        announce.AddChild(AnnounceButton("cadia-panel-announce-chaos", CadiaPanelAction.AnnounceChaos));
        root.AddChild(announce);

        _filter.PlaceHolder = Loc.GetString("cadia-panel-filter");
        _filter.OnTextChanged += _ => Rebuild();
        root.AddChild(_filter);

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_players);
        root.AddChild(scroll);
        Contents.AddChild(root);

        OnClose += () => _entMan.EntityNetManager.SendSystemNetworkMessage(new CadiaPanelSubscribeEvent(false));
        _entMan.EntityNetManager.SendSystemNetworkMessage(new CadiaPanelSubscribeEvent(true));
    }

    private void Send(CadiaPanelAction action, string text = "", Guid? target = null)
    {
        _entMan.EntityNetManager.SendSystemNetworkMessage(new CadiaPanelActionEvent(action, text, target));
    }

    private Button ActionButton(string key, CadiaPanelAction action)
    {
        var button = new Button { Text = Loc.GetString(key), HorizontalExpand = true };
        button.OnPressed += _ => Send(action);
        return button;
    }

    private Button AnnounceButton(string key, CadiaPanelAction action)
    {
        var button = new Button { Text = Loc.GetString(key) };
        button.OnPressed += _ =>
        {
            Send(action, _announce.Text);
            _announce.Text = string.Empty;
        };
        return button;
    }

    private static Control Row(string key, params Control[] buttons)
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        row.AddChild(new Label { Text = Loc.GetString(key), MinWidth = 110, FontColorOverride = Color.FromHex("#e8c66a") });
        foreach (var button in buttons)
        {
            button.HorizontalExpand = true;
            row.AddChild(button);
        }
        return row;
    }

    public void Update(CadiaPanelStateEvent state)
    {
        _state = state;
        _closeImperium.Text = Loc.GetString(state.ImperiumForced ? "cadia-panel-open-imperium" : "cadia-panel-close-imperium");
        _closeChaos.Text = Loc.GetString(state.ChaosForced ? "cadia-panel-open-chaos" : "cadia-panel-close-chaos");
        _veilToggle.Text = Loc.GetString(state.VeilUp ? "cadia-panel-veil-drop" : "cadia-panel-veil-raise");
        _status.Text = !state.Active
            ? Loc.GetString("cadia-panel-no-war")
            : Loc.GetString("cadia-panel-status",
                ("phase", Loc.GetString($"cadia-hud-phase-{state.Phase.ToString().ToLowerInvariant()}")),
                ("time", $"{state.SecondsLeft / 60}:{state.SecondsLeft % 60:00}"),
                ("alive", state.PylonsAlive), ("total", state.PylonsTotal), ("warp", state.Warp),
                ("imperium", state.ImperiumAlive), ("imperiumSeats", state.ImperiumSeats),
                ("chaos", state.ChaosAlive), ("chaosSeats", state.ChaosSeats), ("saved", state.Saved),
                ("veil", Loc.GetString(state.VeilUp ? "cadia-panel-veil-up" : "cadia-panel-veil-down")),
                ("refugees", state.RefugeesSaved), ("refugeesTotal", state.RefugeesTotal));
        Rebuild();
    }

    private void Rebuild()
    {
        if (_state == null)
            return;
        // Пересобирать только при изменениях: иначе кнопка исчезнет между нажатием и отпусканием.
        var filter = _filter.Text.Trim();
        var key = filter + string.Join(';', _state.Players.ConvertAll(p => $"{p.User}{p.Character}{p.Alive}{p.Banned}{p.Role}{p.TeamKills}{p.TeamDamage}"));
        if (key == _shown)
            return;
        _shown = key;

        _players.RemoveAllChildren();
        foreach (var player in _state.Players)
        {
            if (filter.Length > 0 &&
                !player.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                !player.Character.Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;

            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
            var color = player.Faction == CadiaFaction.Imperium ? "#e8c66a" : "#d23b2a";
            row.AddChild(new Label
            {
                Text = $"{(player.Alive ? "●" : "✝")} {player.Character} ({player.Name}) — {player.Role}" +
                       (player.TeamKills > 0 || player.TeamDamage > 0 ? " " + Loc.GetString("cadia-panel-friendly-fire", ("kills", player.TeamKills), ("damage", player.TeamDamage)) : string.Empty),
                FontColorOverride = player.TeamKills > 0 ? Color.FromHex("#ff5a3c") : Color.FromHex(color),
                HorizontalExpand = true,
                ClipText = true,
            });
            var user = player.User;
            var remove = new Button { Text = Loc.GetString("cadia-panel-remove") };
            remove.OnPressed += _ => Send(CadiaPanelAction.RemovePlayer, target: user);
            var ban = new Button { Text = Loc.GetString("cadia-panel-ban") };
            ban.OnPressed += _ => Send(CadiaPanelAction.BanPlayer, target: user);
            row.AddChild(remove);
            row.AddChild(ban);
            _players.AddChild(row);
        }
    }
}

public sealed class CadiaEventPanelClientSystem : EntitySystem
{
    private CadiaEventPanelWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CadiaPanelOpenEvent>(OnOpen);
        SubscribeNetworkEvent<CadiaPanelStateEvent>(OnState);
    }

    private void OnOpen(CadiaPanelOpenEvent ev)
    {
        if (_window != null)
        {
            _window.MoveToFront();
            return;
        }
        _window = new CadiaEventPanelWindow();
        _window.OnClose += () => _window = null;
        _window.OpenCentered();
    }

    private void OnState(CadiaPanelStateEvent ev)
    {
        _window?.Update(ev);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _window?.Close();
    }
}

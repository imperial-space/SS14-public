using System.Linq;
using System.Numerics;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.Cult;
using Content.Shared.Imperial.Cult.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Cult;

/// <summary>
/// Синематики концовки культа, затемнение звёздного света при черчении руны Нар'Си
/// и видимость освящённой земли (blessed_aware).
/// </summary>
public sealed class CultEndingSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IResourceCache _resources = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private static readonly ResPath CinematicRsi = new("/Textures/Imperial/BloodCult/cinematic.rsi");

    private Control? _screen;
    private AnimatedTextureRect? _image;
    private readonly List<(TimeSpan At, string? State)> _schedule = new();
    private TimeSpan _cinematicStart;

    private CultStarlightOverlay _starlight = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CultCinematicEvent>(OnCinematic);
        SubscribeNetworkEvent<CultStarlightEvent>(OnStarlight);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRestart);
        // Синематик уступает место манифесту конца раунда.
        SubscribeNetworkEvent<RoundEndMessageEvent>(_ => CloseCinematic());

        _starlight = new CultStarlightOverlay(_timing);
        _overlays.AddOverlay(_starlight);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_starlight);
        CloseCinematic();
    }

    private void OnRestart(RoundRestartCleanupEvent ev)
    {
        CloseCinematic();
        _starlight.Reset();
    }

    private void OnStarlight(CultStarlightEvent ev)
    {
        _starlight.Start(ev.Summoning);
    }

    #region Синематик

    private float StateLength(string state)
    {
        if (!_resources.TryGetResource<RSIResource>(CinematicRsi, out var rsi))
            return 1f;
        return rsi.RSI.TryGetState(state, out var st) ? st.GetDelays().Sum() : 1f;
    }

    /// <summary>play_cinematic(): flick() показывает состояние на длину анимации, затем icon_state экрана.</summary>
    private void OnCinematic(CultCinematicEvent ev)
    {
        CloseCinematic();
        _schedule.Clear();
        _cinematicStart = _timing.RealTime;

        switch (ev.Cinematic)
        {
            case CultCinematic.Nuke:
            {
                var intro = 3.5f;
                var explode = StateLength("station_explode_fleet_fade_red");
                Flick(0, "intro_cult_fleet");
                Flick(intro, "station_explode_fleet_fade_red");
                _schedule.Add((TimeSpan.FromSeconds(intro + explode), "summary_cult_fleet"));
                break;
            }
            case CultCinematic.Arm:
                Flick(0, "intro_cult");
                _schedule.Add((TimeSpan.FromSeconds(StateLength("intro_cult")), null));
                Flick(7.3f, "station_corrupted");
                _schedule.Add((TimeSpan.FromSeconds(7.3f + StateLength("station_corrupted")), null));
                break;
            case CultCinematic.Fail:
                _schedule.Add((TimeSpan.Zero, "station_intact"));
                break;
        }

        _schedule.Sort((a, b) => a.At.CompareTo(b.At));

        _screen = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.Black },
            HorizontalExpand = true,
            VerticalExpand = true,
            MouseFilter = Control.MouseFilterMode.Stop,
        };
        _image = new AnimatedTextureRect
        {
            HorizontalAlignment = Control.HAlignment.Center,
            VerticalAlignment = Control.VAlignment.Center,
        };
        _image.DisplayRect.TextureScale = new Vector2(1.5f, 1.5f);
        _screen.AddChild(_image);
        _ui.PopupRoot.AddChild(_screen);
        LayoutContainer.SetAnchorPreset(_screen, LayoutContainer.LayoutPreset.Wide);
        _image.Visible = false;
    }

    private void Flick(float at, string state)
    {
        _schedule.Add((TimeSpan.FromSeconds(at), state));
    }

    private void CloseCinematic()
    {
        _screen?.Orphan();
        _screen = null;
        _image = null;
        _schedule.Clear();
    }

    #endregion

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_image != null && _schedule.Count > 0)
        {
            var elapsed = _timing.RealTime - _cinematicStart;
            while (_schedule.Count > 0 && elapsed >= _schedule[0].At)
            {
                var state = _schedule[0].State;
                _schedule.RemoveAt(0);
                if (state == null)
                {
                    _image.Visible = false;
                    continue;
                }
                _image.SetFromSpriteSpecifier(new SpriteSpecifier.Rsi(CinematicRsi, state));
                _image.Visible = true;
            }
        }

        // blessed_aware: святые, еретики и призраки-конструкты видят освящённую землю.
        var local = _player.LocalEntity;
        var sees = local != null && (CultForeignComponents.Has(EntityManager, local.Value, CultForeignComponents.Holy) || CultForeignComponents.Has(EntityManager, local.Value, CultForeignComponents.Heretic) || HasComp<CultSeeBlessedComponent>(local));
        var query = EntityQueryEnumerator<CultBlessingComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite))
            _sprite.LayerSetVisible((uid, sprite), CultVisualLayers.Base, sees);
    }
}

/// <summary>
/// set_starlight(): за 9 шагов по 8 с звёзды тускнеют (насыщенность 0.4, яркость 0.5) и краснеют до #c21d57;
/// при провале — 4 шага по 8 с обратно. Рисуется под миром, поэтому окрашивает только космос.
/// </summary>
public sealed class CultStarlightOverlay : Overlay
{
    private readonly IGameTiming _timing;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    private static readonly Color Mid = new(0f, 0f, 0f, 0.5f);
    private static readonly Color End = Color.FromHex("#c21d5780");

    private Color _from = Color.Transparent;
    private Color _current = Color.Transparent;
    private bool _summoning;
    private TimeSpan _start;
    private bool _active;

    public CultStarlightOverlay(IGameTiming timing)
    {
        _timing = timing;
        ZIndex = 1;
    }

    public void Start(bool summoning)
    {
        _from = _current;
        _summoning = summoning;
        _start = _timing.RealTime;
        _active = true;
    }

    public void Reset()
    {
        _current = Color.Transparent;
        _active = false;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_active)
        {
            var t = (float) (_timing.RealTime - _start).TotalSeconds;
            if (_summoning)
            {
                // hsv_gradient(i, 1, start, 3, mid, 6, mid, 9, end), шаги по 8 с.
                var step = Math.Clamp(MathF.Floor(t / 8f) + 1, 1, 9);
                _current = step <= 3
                    ? Color.InterpolateBetween(_from, Mid, (step - 1) / 2f)
                    : step <= 6 ? Mid : Color.InterpolateBetween(Mid, End, (step - 6) / 3f);
            }
            else
            {
                var step = Math.Clamp(MathF.Floor(t / 8f) + 1, 1, 4);
                _current = Color.InterpolateBetween(_from, Color.Transparent, step / 4f);
                if (step >= 4)
                    _active = false;
            }
        }

        if (_current.A <= 0.001f)
            return;
        args.WorldHandle.DrawRect(args.WorldBounds, _current);
    }
}

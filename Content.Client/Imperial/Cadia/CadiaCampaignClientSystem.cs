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

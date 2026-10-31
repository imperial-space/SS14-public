using System.Numerics;
using Content.Shared.Imperial.Cadia;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Cadia;

/// <summary>Шкалы псайкера над панелью действий: варп-заряд и напряжение, чем оно выше — тем опаснее каст.</summary>
public sealed class CadiaPsykerHudSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;

    private CadiaPsykerHudOverlay? _hud;

    public override void Initialize()
    {
        base.Initialize();
        _hud = new CadiaPsykerHudOverlay();
        _overlays.AddOverlay(_hud);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_hud != null)
            _overlays.RemoveOverlay(_hud);
    }
}

public sealed class CadiaPsykerHudOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly Color Charge = Color.FromHex("#6ab8ff");
    private static readonly Color StrainLow = Color.FromHex("#ffd37a");
    private static readonly Color StrainHigh = Color.FromHex("#ff3a2a");
    private static readonly Color Back = Color.FromHex("#0b0d0ad8");

    private readonly Font _font;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CadiaPsykerHudOverlay()
    {
        IoCManager.InjectDependencies(this);
        _font = new VectorFont(IoCManager.Resolve<IResourceCache>().GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 11);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local || !_entMan.TryGetComponent<CadiaPsykerComponent>(local, out var psyker))
            return;

        var handle = args.ScreenHandle;
        var origin = new Vector2(args.ViewportBounds.Left + 12, args.ViewportBounds.Bottom - 64);
        handle.DrawRect(UIBox2.FromDimensions(origin, new Vector2(196, 52)), Back);

        var share = psyker.MaxCharge > 0 ? Math.Clamp(psyker.Charge / psyker.MaxCharge, 0f, 1f) : 0f;
        DrawBar(handle, origin + new Vector2(6, 6), share, Charge,
            Loc.GetString("cadia-psy-hud-charge", ("charge", (int) psyker.Charge), ("max", (int) psyker.MaxCharge)));

        var strain = Math.Clamp(psyker.Strain / 100f, 0f, 1f);
        var silenced = _timing.CurTime < psyker.SilencedUntil;
        DrawBar(handle, origin + new Vector2(6, 29), strain, Color.InterpolateBetween(StrainLow, StrainHigh, strain),
            silenced ? Loc.GetString("cadia-psy-hud-silenced") : Loc.GetString("cadia-psy-hud-strain", ("strain", (int) psyker.Strain)));
    }

    private void DrawBar(DrawingHandleScreen handle, Vector2 at, float share, Color color, string text)
    {
        var box = UIBox2.FromDimensions(at, new Vector2(184, 17));
        handle.DrawRect(box, Color.FromHex("#22241e"));
        handle.DrawRect(UIBox2.FromDimensions(at, new Vector2(184 * share, 17)), color.WithAlpha(0.75f));
        handle.DrawRect(box, color.WithAlpha(0.6f), false);
        handle.DrawString(_font, at + new Vector2(5, 1), text, Color.White);
    }
}

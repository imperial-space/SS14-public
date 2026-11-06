using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Imperial.ZLevels;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Audio.Components;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Cadia;

/// <summary>Шкала морали в верхнем левом углу: космодесант страха не ведает и шкалы не видит.</summary>
public sealed class CadiaMoraleHudOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly Color VeilColor = Color.FromHex("#5affc8");
    private static readonly Color VeilEnding = Color.FromHex("#ff5a3a");
    /// <summary>В последние секунды отсчёт краснеет — сейчас начнётся обстрел.</summary>
    private static readonly TimeSpan VeilWarning = TimeSpan.FromSeconds(30);

    private static readonly Color Steady = Color.FromHex("#5aff78");
    private static readonly Color Shaken = Color.FromHex("#ffd37a");
    private static readonly Color Broken = Color.FromHex("#ff3a2a");
    private const float BarWidth = 470f;

    private readonly Font _font;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CadiaMoraleHudOverlay()
    {
        IoCManager.InjectDependencies(this);
        _font = new VectorFont(IoCManager.Resolve<IResourceCache>().GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 11);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        DrawMorale(args);
        DrawVeil(args.ScreenHandle, args.ViewportBounds);
    }

    private void DrawMorale(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local ||
            !_entMan.TryGetComponent<CadiaMoraleComponent>(local, out var morale) ||
            _entMan.HasComponent<CadiaAstartesComponent>(local) ||
            !_entMan.TryGetComponent<CadiaFactionMemberComponent>(local, out var member) || member.Role == null)
            return;

        var value = morale.Morale;
        var color = value < CadiaMoraleComponent.BrokenBelow ? Broken : value < CadiaMoraleComponent.ShakenBelow ? Shaken : Steady;
        var key = value < CadiaMoraleComponent.BrokenBelow ? "cadia-morale-hud-broken" : value < CadiaMoraleComponent.ShakenBelow ? "cadia-morale-hud-shaken" : "cadia-morale-hud-steady";

        var handle = args.ScreenHandle;
        // Под панелью войны: левый верхний угол закрывает панель действий.
        var at = new Vector2(args.ViewportBounds.Left + (args.ViewportBounds.Width - BarWidth) / 2f, args.ViewportBounds.Top + 106);
        var box = UIBox2.FromDimensions(at, new Vector2(BarWidth, 17));
        handle.DrawRect(box, Color.FromHex("#0b0d0ad8"));
        handle.DrawRect(UIBox2.FromDimensions(at, new Vector2(BarWidth * Math.Clamp(value / 100f, 0f, 1f), 17)), color.WithAlpha(0.45f));
        handle.DrawRect(box, color.WithAlpha(0.7f), false);
        var text = Loc.GetString(key, ("value", (int)value));
        handle.DrawString(_font, at + new Vector2(6, 2), text, Color.Black);
        handle.DrawString(_font, at + new Vector2(5, 1), text, Color.White);
    }

    /// <summary>
    /// У правого края шкалы морали: сколько ещё держится завеса пилонов (или что её держит ведущий). Ниже шкалы — строка
    /// цели, поэтому таймер живёт в той же строке, что и мораль; у Астартес без шкалы — своя подложка на её месте.
    /// </summary>
    private void DrawVeil(DrawingHandleScreen handle, UIBox2i bounds)
    {
        if (_player.LocalEntity is not { } local ||
            !_entMan.TryGetComponent<CadiaFactionMemberComponent>(local, out var member) || member.Role == null)
            return;
        var wars = _entMan.EntityQueryEnumerator<CadiaWarStateComponent>();
        while (wars.MoveNext(out var war))
        {
            if (!CadiaPylonVeilSystem.VeilUp(war))
                continue;
            var color = VeilColor;
            string text;
            if (war.VeilOverride == null)
            {
                var left = war.PhaseEnd - _timing.CurTime;
                if (left < TimeSpan.Zero)
                    left = TimeSpan.Zero;
                if (left <= VeilWarning)
                    color = VeilEnding;
                text = Loc.GetString("cadia-veil-hud-timer", ("time", $"{(int)left.TotalMinutes}:{left.Seconds:00}"));
            }
            else
            {
                text = Loc.GetString("cadia-veil-hud-held");
            }
            var size = handle.GetDimensions(_font, text, 1f);
            var bar = new Vector2(bounds.Left + (bounds.Width - BarWidth) / 2f, bounds.Top + 106);
            var at = new Vector2(bar.X + BarWidth - size.X - 6, bar.Y + 1);
            if (!_entMan.HasComponent<CadiaMoraleComponent>(local) || _entMan.HasComponent<CadiaAstartesComponent>(local))
                handle.DrawRect(UIBox2.FromDimensions(at - new Vector2(6, 1), size + new Vector2(12, 2)), Color.FromHex("#0b0d0ad8"));
            handle.DrawString(_font, at + new Vector2(1, 1), text, Color.Black);
            handle.DrawString(_font, at, text, color);
            return;
        }
    }
}

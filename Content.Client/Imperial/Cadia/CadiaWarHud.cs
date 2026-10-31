using System.Numerics;
using Content.Shared.Imperial.Cadia;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Cadia;

/// <summary>
/// Панель войны вверху экрана: эмблема стороны, фаза и таймер, пилоны, захват цитадели, подкрепления и сила варпа.
/// </summary>
public sealed class CadiaWarHudOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly Color Gold = Color.FromHex("#e8c66a");
    private static readonly Color Blood = Color.FromHex("#d23b2a");
    private static readonly Color Rune = Color.FromHex("#3dff8c");
    private static readonly Color Warp = Color.FromHex("#c66bff");
    private static readonly Color Panel = Color.FromHex("#0b0d0ae0");
    private static readonly Color Dim = Color.FromHex("#9a9a8a");

    private const float Width = 470f;
    private const float Height = 78f;

    private readonly Font _title;
    private readonly Font _font;
    private readonly Font _small;
    private readonly Texture _aquila;
    private readonly Texture _chaos;
    private readonly Texture _pylon;
    private readonly Texture _ruins;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CadiaWarHudOverlay()
    {
        IoCManager.InjectDependencies(this);
        var cache = IoCManager.Resolve<IResourceCache>();
        _title = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 15);
        _font = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 11);
        _small = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 10);
        var sprite = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<SpriteSystem>();
        var icons = new ResPath("/Textures/Imperial/Cadia/War/icons.rsi");
        var pylon = new ResPath("/Textures/Imperial/Cadia/War/pylon.rsi");
        // Эмблема панели — отдельная, 64×64: у значка аквилы 32×32 кончики крыльев упираются в края кадра.
        _aquila = sprite.Frame0(new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/Cadia/War/emblem.rsi"), "aquila"));
        _chaos = sprite.Frame0(new SpriteSpecifier.Rsi(icons, "chaos_star"));
        _pylon = sprite.Frame0(new SpriteSpecifier.Rsi(pylon, "icon"));
        _ruins = sprite.Frame0(new SpriteSpecifier.Rsi(pylon, "ruins"));
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
        if (war == null)
            return;

        CadiaFaction? side = null;
        if (_player.LocalEntity is { } local &&
            _entMan.TryGetComponent<CadiaFactionMemberComponent>(local, out var member) && member.Role != null)
            side = member.Faction;

        var accent = side == CadiaFaction.Chaos ? Blood : Gold;
        var handle = args.ScreenHandle;
        var origin = new Vector2(args.ViewportBounds.Left + (args.ViewportBounds.Width - Width) / 2f, args.ViewportBounds.Top + 8f);
        var box = UIBox2.FromDimensions(origin, new Vector2(Width, Height));

        handle.DrawRect(box, Panel);
        handle.DrawRect(box, accent, false);
        handle.DrawRect(new UIBox2(box.Left + 2, box.Top + 2, box.Right - 2, box.Bottom - 2), accent.WithAlpha(0.35f), false);

        // Эмблема стороны: аквила для Империума и наблюдателей, звезда Хаоса — для еретиков.
        var emblem = side == CadiaFaction.Chaos ? _chaos : _aquila;
        handle.DrawTextureRect(emblem, UIBox2.FromDimensions(origin + new Vector2(8, 11), new Vector2(56, 56)));

        var now = _timing.CurTime;
        var x = origin.X + 72f;

        if (war.Phase == CadiaWarPhase.Ended)
        {
            var winText = Loc.GetString(war.Winner == CadiaFaction.Chaos ? "cadia-hud-chaos-won" : "cadia-hud-imperium-won");
            var winColor = war.Winner == CadiaFaction.Chaos ? Blood : Gold;
            handle.DrawString(_title, new Vector2(x, origin.Y + 18), winText, winColor);
            handle.DrawString(_small, new Vector2(x, origin.Y + 44), Loc.GetString("cadia-hud-ended"), Dim);
            return;
        }

        // Строка 1: фаза и обратный отсчёт; в последнюю минуту таймер мигает.
        var phase = Loc.GetString($"cadia-hud-phase-{war.Phase.ToString().ToLowerInvariant()}");
        handle.DrawString(_title, new Vector2(x, origin.Y + 6), phase, accent);
        var left = war.PhaseEnd - now;
        if (left < TimeSpan.Zero)
            left = TimeSpan.Zero;
        var timer = $"{(int) left.TotalMinutes:00}:{left.Seconds:00}";
        var urgent = left.TotalSeconds < 60 && (int) (now.TotalSeconds * 2) % 2 == 0;
        var timerSize = handle.GetDimensions(_title, timer, 1f);
        handle.DrawString(_title, new Vector2(box.Right - timerSize.X - 12, origin.Y + 6), timer, urgent ? Blood : Color.White);

        // Строка 2: пилоны — у каждого своя ячейка: живой стоит на зелёной подсветке, павший лежит руинами на красной.
        // Обелиск высокий и узкий: рисуем его крупнее ячейки, чтобы он читался, а ячейки — без наложений.
        var y = origin.Y + 30;
        const float cell = 20f;
        const float cellGap = 2f;
        for (var i = 0; i < war.PylonsTotal; i++)
        {
            var alive = i < war.PylonsAlive;
            var cellBox = UIBox2.FromDimensions(new Vector2(x + i * (cell + cellGap), y - 3), new Vector2(cell, cell));
            handle.DrawRect(cellBox, (alive ? Rune : Blood).WithAlpha(0.16f));
            handle.DrawRect(cellBox, (alive ? Rune : Blood).WithAlpha(0.55f), false);
            var icon = UIBox2.FromDimensions(cellBox.Center - new Vector2(13, 14), new Vector2(26, 26));
            handle.DrawTextureRect(alive ? _pylon : _ruins, icon, alive ? Color.White : Color.White.WithAlpha(0.7f));
        }
        if (war.Phase == CadiaWarPhase.Evacuation)
        {
            var transport = war.NextTransport - now;
            if (transport < TimeSpan.Zero)
                transport = TimeSpan.Zero;
            handle.DrawString(_small, new Vector2(x, y + 20),
                Loc.GetString("cadia-hud-evac", ("saved", war.EvacSaved), ("time", $"{(int) transport.TotalMinutes}:{transport.Seconds:00}")), Gold);
        }
        else
        {
            var pylonText = Loc.GetString("cadia-hud-pylons", ("alive", war.PylonsAlive), ("total", war.PylonsTotal));
            handle.DrawString(_small, new Vector2(x, y + 20), pylonText, war.PylonsAlive > 0 ? Rune : Blood);
        }

        // Цитадель: полоса захвата. Оспаривается — мигает оранжевым.
        var barX = x + 190;
        var barW = Width - (barX - origin.X) - 14;
        handle.DrawString(_font, new Vector2(barX, y - 2), Loc.GetString("cadia-hud-citadel"), Color.White);
        var bar = UIBox2.FromDimensions(new Vector2(barX, y + 14), new Vector2(barW, 9));
        handle.DrawRect(bar, Color.FromHex("#2a2a24"));
        var fill = war.CitadelContested && (int) (now.TotalSeconds * 3) % 2 == 0 ? Color.FromHex("#ff9a3c") : Blood;
        if (war.CitadelProgress > 0)
            handle.DrawRect(UIBox2.FromDimensions(bar.TopLeft, new Vector2(barW * war.CitadelProgress, 9)), fill);
        handle.DrawRect(bar, Gold.WithAlpha(0.6f), false);
        var pct = $"{(int) (war.CitadelProgress * 100)}%";
        handle.DrawString(_small, new Vector2(bar.Right - handle.GetDimensions(_small, pct, 1f).X, y - 1), pct, war.CitadelProgress > 0 ? Blood : Dim);

        // Строка 3: подкрепления своей стороны и сила варпа.
        var y3 = origin.Y + Height - 17;
        var wave = side == CadiaFaction.Chaos ? war.NextChaosWave : war.NextImperiumWave;
        var waveLeft = wave - now;
        if (waveLeft < TimeSpan.Zero)
            waveLeft = TimeSpan.Zero;
        var warp = Loc.GetString("cadia-hud-warp", ("power", war.WarpPower));
        var warpX = box.Right - handle.GetDimensions(_small, warp, 1f).X - 12;
        // Подпись волны не должна наезжать на варп: если места мало — короткая форма.
        var time = $"{(int) waveLeft.TotalMinutes}:{waveLeft.Seconds:00}";
        // Полная подпись не помещается перед варпом на любом масштабе — всегда короткая.
        var waveText = Loc.GetString("cadia-hud-wave-short", ("time", time));
        handle.DrawString(_small, new Vector2(barX, y3), waveText, Dim);
        handle.DrawString(_small, new Vector2(warpX, y3), warp, Warp);

        DrawSectors(handle, war, origin, now);
    }

    /// <summary>Полоса секторов под панелью: цвет хозяина, поверх него ход захвата, мигание — идёт бой.</summary>
    private void DrawSectors(DrawingHandleScreen handle, CadiaWarStateComponent war, Vector2 origin, TimeSpan now)
    {
        if (war.Sectors.Count == 0)
            return;
        const float gap = 4f;
        var chip = (Width - gap * (war.Sectors.Count - 1)) / war.Sectors.Count;
        var top = origin.Y + Height + 2f;
        for (var i = 0; i < war.Sectors.Count; i++)
        {
            var sector = war.Sectors[i];
            var rect = UIBox2.FromDimensions(new Vector2(origin.X + i * (chip + gap), top), new Vector2(chip, 16f));
            handle.DrawRect(rect, Panel);
            handle.DrawRect(rect, SideColor(sector.Owner).WithAlpha(0.45f));
            if (sector.Capturer is { } capturer && sector.Progress > 0f)
                handle.DrawRect(UIBox2.FromDimensions(rect.TopLeft, new Vector2(chip * Math.Clamp(sector.Progress, 0f, 1f), 16f)), SideColor(capturer).WithAlpha(0.8f));
            var flash = sector.Contested && (int) (now.TotalSeconds * 3) % 2 == 0;
            handle.DrawRect(rect, flash ? Color.FromHex("#ff9a3c") : SideColor(sector.Owner), false);
            var size = handle.GetDimensions(_font, sector.Letter, 1f);
            handle.DrawString(_font, new Vector2(rect.Center.X - size.X / 2f, top), sector.Letter, Color.White);
        }
    }

    private static Color SideColor(CadiaFaction? side) => side switch
    {
        CadiaFaction.Imperium => Gold,
        CadiaFaction.Chaos => Blood,
        _ => Dim,
    };
}

public sealed class CadiaWarHudSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;

    private CadiaWarHudOverlay? _hud;

    public override void Initialize()
    {
        base.Initialize();
        _hud = new CadiaWarHudOverlay();
        _overlay.AddOverlay(_hud);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_hud != null)
            _overlay.RemoveOverlay(_hud);
    }
}

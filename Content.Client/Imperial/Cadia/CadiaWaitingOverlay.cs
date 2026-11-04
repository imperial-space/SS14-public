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

using System.Numerics;
using Content.Shared.Imperial.Cadia;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Cadia;

public sealed class CadiaShipGunHudOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private readonly Font _font;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CadiaShipGunHudOverlay()
    {
        IoCManager.InjectDependencies(this);
        _font = new VectorFont(IoCManager.Resolve<IResourceCache>().GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 13);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local || !_entMan.TryGetComponent<CadiaShipGunnerComponent>(local, out var gunner))
            return;

        var guns = 0;
        var ammo = 0;
        var gunQuery = _entMan.EntityQueryEnumerator<CadiaShipGunComponent, TransformComponent>();
        while (gunQuery.MoveNext(out var gun, out var xform))
        {
            if (xform.GridUid != gunner.Grid)
                continue;
            guns++;
            ammo += gun.Ammo;
        }
        var bays = 0;
        var bombs = 0;
        var bayQuery = _entMan.EntityQueryEnumerator<CadiaShipBombBayComponent, TransformComponent>();
        while (bayQuery.MoveNext(out var bay, out var xform))
        {
            if (xform.GridUid != gunner.Grid)
                continue;
            bays++;
            bombs += bay.Bombs;
        }

        var selected = gunner.Selected is { } gunUid && _entMan.TryGetComponent<CadiaShipGunComponent>(gunUid, out var chosen)
            ? Loc.GetString("cadia-ship-hud-selected", ("gun", _entMan.GetComponent<MetaDataComponent>(gunUid).EntityName), ("ammo", chosen.Ammo))
            : Loc.GetString("cadia-ship-hud-selected-all");

        var handle = args.ScreenHandle;
        var box = UIBox2.FromDimensions(new Vector2(args.ViewportBounds.Center.X - 200, args.ViewportBounds.Top + 148), new Vector2(400, 74));
        var amber = Color.FromHex("#ffd37a");
        handle.DrawRect(box, Color.FromHex("#0b0d0ad8"));
        handle.DrawRect(box, amber, false);
        handle.DrawString(_font, box.TopLeft + new Vector2(10, 6), Loc.GetString("cadia-ship-hud-guns", ("guns", guns), ("ammo", ammo)), amber);
        handle.DrawString(_font, box.TopLeft + new Vector2(10, 27), Loc.GetString("cadia-ship-hud-bombs", ("bays", bays), ("bombs", bombs)), amber);
        handle.DrawString(_font, box.TopLeft + new Vector2(10, 48), selected, Color.FromHex("#5aff78"));
    }
}

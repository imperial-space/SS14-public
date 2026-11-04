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

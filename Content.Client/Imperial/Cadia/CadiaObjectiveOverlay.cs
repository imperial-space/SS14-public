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
        var (text, target) = Pick(local, member.Faction, war, here, (int)xform.MapID, xform.MapUid);

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
            line = Loc.GetString("cadia-objective-line-distance", ("text", text), ("meters", (int)delta.Length()));

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

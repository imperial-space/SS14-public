using Content.Shared.Buckle.Components;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Imperial.ZLevels;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.ZLevels;

/// <summary>
/// Трапы SS13 (/obj/structure/ladder): связываются с трапом ровно этажом выше и ниже в той же клетке.
/// Клик — туда, куда ведёт (если в обе стороны — радиальное меню выбора), Alt-клик — спуститься;
/// оба действия есть и в контекстном меню.
/// Подъём занимает travel_time и тянет за собой тех, кого тащишь.
/// </summary>
public sealed class ZLadderSystem : EntitySystem
{
    [Dependency] private readonly ZLevelSystem _z = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private static readonly SpriteSpecifier UpIcon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/ZLevels/ladder.rsi"), "ladder10");
    private static readonly SpriteSpecifier DownIcon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/ZLevels/ladder.rsi"), "ladder01");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ZLadderComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ZLadderComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ZLadderComponent, AnchorStateChangedEvent>(OnAnchor);
        SubscribeLocalEvent<ZLadderComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<ZLadderComponent, GetVerbsEvent<AlternativeVerb>>(OnAltVerbs);
        SubscribeLocalEvent<ZLadderComponent, GetVerbsEvent<InteractionVerb>>(OnVerbs);
        SubscribeLocalEvent<ZLadderComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<ZLadderComponent, ZLadderClimbDoAfterEvent>(OnClimbed);
        SubscribeLocalEvent<ZLadderComponent, ZLadderChoiceMessage>(OnChoice);
        SubscribeLocalEvent<ZLevelsRelinkedEvent>(OnRelinked);
    }

    // ---------- Связывание (LateInitialize) ----------

    private void OnMapInit(Entity<ZLadderComponent> ent, ref MapInitEvent args) => Link(ent);

    private void OnAnchor(Entity<ZLadderComponent> ent, ref AnchorStateChangedEvent args)
    {
        Disconnect(ent);
        if (args.Anchored)
            Link(ent);
    }

    private void OnShutdown(Entity<ZLadderComponent> ent, ref ComponentShutdown args) => Disconnect(ent);

    private void OnRelinked(ZLevelsRelinkedEvent args)
    {
        foreach (var level in _z.GetStack(args.Stack))
        {
            var mapId = Comp<MapComponent>(level).MapId;
            var query = EntityQueryEnumerator<ZLadderComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var ladder, out var xform))
            {
                if (xform.MapID == mapId)
                    Link((uid, ladder));
            }
        }
    }

    /// <summary>Ищет трап прямо над и прямо под собой.</summary>
    public void Link(Entity<ZLadderComponent> ent)
    {
        if (!Transform(ent).Anchored)
            return;
        var here = _z.SnapToTile(_xform.GetMapCoordinates(ent));
        if (ent.Comp.Down == null && FindLadder(here, false, ent.Comp.Kind) is { } down)
            LinkPair(ent, down);
        if (ent.Comp.Up == null && FindLadder(here, true, ent.Comp.Kind) is { } up)
            LinkPair(up, ent);
        UpdateAppearance(ent);
    }

    private Entity<ZLadderComponent>? FindLadder(MapCoordinates here, bool up, string kind)
    {
        if (_z.GetStep(here, up) is not { } other)
            return null;
        foreach (var e in _z.GetAnchored(other))
        {
            if (TryComp<ZLadderComponent>(e, out var ladder) && ladder.Kind == kind)
                return (e, ladder);
        }
        return null;
    }

    /// <summary>link_down: upper.Down = lower, lower.Up = upper.</summary>
    private void LinkPair(Entity<ZLadderComponent> upper, Entity<ZLadderComponent> lower)
    {
        upper.Comp.Down = lower;
        lower.Comp.Up = upper;
        Dirty(upper);
        Dirty(lower);
        UpdateAppearance(upper);
        UpdateAppearance(lower);
    }

    private void Disconnect(Entity<ZLadderComponent> ent)
    {
        if (ent.Comp.Down is { } down && TryComp<ZLadderComponent>(down, out var downLadder))
        {
            downLadder.Up = null;
            Dirty(down, downLadder);
            UpdateAppearance((down, downLadder));
        }
        if (ent.Comp.Up is { } up && TryComp<ZLadderComponent>(up, out var upLadder))
        {
            upLadder.Down = null;
            Dirty(up, upLadder);
            UpdateAppearance((up, upLadder));
        }
        ent.Comp.Up = null;
        ent.Comp.Down = null;
        Dirty(ent);
    }

    /// <summary>update_icon_state: ladder{есть верх}{есть низ}.</summary>
    private void UpdateAppearance(Entity<ZLadderComponent> ent)
    {
        if (TerminatingOrDeleted(ent))
            return;
        var state = $"ladder{(ent.Comp.Up != null ? 1 : 0)}{(ent.Comp.Down != null ? 1 : 0)}";
        _appearance.SetData(ent, ZLadderVisuals.State, state);
    }

    // ---------- Использование ----------

    private void OnActivate(Entity<ZLadderComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;
        args.Handled = true;
        // Можно и вверх, и вниз — радиальное меню выбора (как в SS13); иначе сразу туда, куда ведёт.
        if (ent.Comp.Up != null && ent.Comp.Down != null && _ui.HasUi(ent, ZLadderUiKey.Key))
        {
            _ui.OpenUi(ent.Owner, ZLadderUiKey.Key, args.User);
            return;
        }
        Use(ent, args.User, ent.Comp.Up != null || ent.Comp.Down == null);
    }

    private void OnChoice(Entity<ZLadderComponent> ent, ref ZLadderChoiceMessage args)
    {
        _ui.CloseUi(ent.Owner, ZLadderUiKey.Key, args.Actor);
        Use(ent, args.Actor, args.Up);
    }

    private void OnAltVerbs(Entity<ZLadderComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Down == null)
            return;
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString($"{ent.Comp.LocPrefix}-climb-down"),
            Icon = DownIcon,
            Act = () => Use(ent, user, false),
        });
    }

    private void OnVerbs(Entity<ZLadderComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;
        var user = args.User;
        if (ent.Comp.Up != null)
        {
            args.Verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString($"{ent.Comp.LocPrefix}-climb-up"),
                Icon = UpIcon,
                Act = () => Use(ent, user, true),
            });
        }
        if (ent.Comp.Down != null)
        {
            args.Verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString($"{ent.Comp.LocPrefix}-climb-down"),
                Icon = DownIcon,
                Act = () => Use(ent, user, false),
            });
        }
    }

    private void OnExamine(Entity<ZLadderComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString($"{ent.Comp.LocPrefix}-examine"));
    }

    /// <summary>use: проверки и начало подъёма.</summary>
    public void Use(Entity<ZLadderComponent> ent, EntityUid user, bool goingUp)
    {
        if (ent.Comp.Up == null && ent.Comp.Down == null)
        {
            _popup.PopupEntity(Loc.GetString($"{ent.Comp.LocPrefix}-nowhere"), ent, user);
            return;
        }
        if (goingUp ? ent.Comp.Up == null : ent.Comp.Down == null)
        {
            _popup.PopupEntity(Loc.GetString(goingUp ? $"{ent.Comp.LocPrefix}-no-further-up" : $"{ent.Comp.LocPrefix}-no-further-down"), ent, user);
            return;
        }
        if (TryComp<BuckleComponent>(user, out var buckle) && buckle.BuckledTo is { } strap && Transform(strap).Anchored)
        {
            _popup.PopupEntity(Loc.GetString($"{ent.Comp.LocPrefix}-buckled"), ent, user);
            return;
        }

        _popup.PopupEntity(Loc.GetString(goingUp ? $"{ent.Comp.LocPrefix}-climbing-up" : $"{ent.Comp.LocPrefix}-climbing-down"), user, PopupType.Small);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.TravelTime, new ZLadderClimbDoAfterEvent(goingUp), ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            DistanceThreshold = 1.5f,
        });
    }

    private void OnClimbed(Entity<ZLadderComponent> ent, ref ZLadderClimbDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;
        args.Handled = true;
        Travel(ent, args.User, args.GoingUp);
    }

    /// <summary>travel: zMove к парному трапу вместе с теми, кого тащишь (ZMOVE_INCLUDE_PULLED).</summary>
    public bool Travel(Entity<ZLadderComponent> ent, EntityUid user, bool goingUp)
    {
        var other = goingUp ? ent.Comp.Up : ent.Comp.Down;
        if (other == null || TerminatingOrDeleted(other))
        {
            _popup.PopupEntity(Loc.GetString($"{ent.Comp.LocPrefix}-nothing-that-way"), ent, user);
            return false;
        }

        var target = _xform.GetMapCoordinates(other.Value);
        _popup.PopupEntity(Loc.GetString(goingUp ? $"{ent.Comp.LocPrefix}-climbs-up" : $"{ent.Comp.LocPrefix}-climbs-down", ("user", Name(user)), ("ladder", Name(ent))), ent, PopupType.Small);
        _z.ZMove(user, target, ent.Comp.IncludePulled);
        _popup.PopupEntity(Loc.GetString(goingUp ? $"{ent.Comp.LocPrefix}-climbed-up" : $"{ent.Comp.LocPrefix}-climbed-down"), user, PopupType.Small);
        return true;
    }
}

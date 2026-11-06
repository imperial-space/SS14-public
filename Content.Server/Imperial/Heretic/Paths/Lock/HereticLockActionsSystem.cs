using Content.Server.Polymorph.Systems;
using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Paths.Lock;
using Content.Shared.Polymorph;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Heretic.Paths.Lock;

/// <summary>
/// Способности пути Замка.
/// </summary>
public sealed class HereticLockActionsSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly HereticSystem _heretic = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private static readonly EntProtoId LockShapeshiftProto = "ActionHereticLockShapeshift";

    private static readonly EntProtoId HereticLockTear = "HereticLockTear";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HereticComponent, HereticLockShapeshiftActionEvent>(OnLockShapeshift);
        SubscribeLocalEvent<HereticKnowledgeHolderComponent, HereticLockShapeshiftSelectMessage>(OnLockShapeshiftSelect);
        SubscribeLocalEvent<HereticPathAscendedEvent>(OnPathAscended);
    }

    // ── Lock: Шейпшифт возвышения ─────────────────────────────────────────────

    private static readonly Dictionary<HereticLockShapeshiftCreature, ProtoId<PolymorphPrototype>> LockShapeshiftProtos = new()
    {
        { HereticLockShapeshiftCreature.RustWalker, "HereticLockPolymorph_RustWalker" },
        { HereticLockShapeshiftCreature.AshOrb,     "HereticLockPolymorph_AshOrb"     },
        { HereticLockShapeshiftCreature.FleshWorm,  "HereticLockPolymorph_FleshWorm"  },
        { HereticLockShapeshiftCreature.RawProphet, "HereticLockPolymorph_RawProphet" },
    };

    private void OnLockShapeshift(EntityUid uid, HereticComponent comp, HereticLockShapeshiftActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        if (!HasComp<HereticLockAscendedComponent>(uid))
            return;

        args.Handled = true;

        if (comp.BuiHolder is { } holder)
            _ui.TryOpenUi(holder, HereticLockShapeshiftUiKey.Key, uid);
    }

    private void OnLockShapeshiftSelect(EntityUid holderUid, HereticKnowledgeHolderComponent holderComp, HereticLockShapeshiftSelectMessage args)
    {
        var uid = args.Actor;
        if (!HasComp<HereticComponent>(uid))
            return;
        if (!HasComp<HereticLockAscendedComponent>(uid))
            return;

        if (!LockShapeshiftProtos.TryGetValue(args.Creature, out var proto))
            return;

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_ambience_antag_heretic_ascend_knock.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-lock-shapeshift-activate"), uid, uid, PopupType.Large);
        _polymorph.PolymorphEntity(uid, proto);
    }

    private void OnPathAscended(ref HereticPathAscendedEvent args)
    {
        if (args.Component.CurrentPath != HereticPath.Lock)
            return;

        var uid = args.Heretic;
        var coords = Transform(uid).Coordinates;

        EnsureComp<HereticLockAscendedComponent>(uid);
        var tearUid = Spawn(HereticLockTear, coords);
        var tearComp = EnsureComp<HereticLockTearComponent>(tearUid);
        tearComp.Master = uid;
        _actions.AddAction(uid, LockShapeshiftProto);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/ascend_knock.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-ascension-lock"), uid, uid, PopupType.Large);
    }
}

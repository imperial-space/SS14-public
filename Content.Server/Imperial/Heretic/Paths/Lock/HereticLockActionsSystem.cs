using System.Numerics;
using System.Threading;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Beam;
using Content.Server.Body;
using Content.Server.Body.Components;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Damage.Systems;
using Content.Server.Decals;
using Content.Server.DoAfter;
using Content.Server.Doors.Systems;
using Content.Server.Mind;
using Content.Server.Polymorph.Systems;
using Content.Server.Popups;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.CombatMode;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Electrocution;
using Content.Shared.Eye;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Paths.Lock;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Medical;
using Content.Shared.Mind.Components;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Polymorph;
using Content.Shared.Popups;
using Content.Shared.SSDIndicator;
using Content.Shared.Slippery;
using Content.Shared.Speech.Muting;
using Content.Shared.Stacks;
using Content.Shared.StatusEffect;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Temperature.Components;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Reflect;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;
using NewStatusEffectsSystem = Content.Shared.StatusEffectNew.StatusEffectsSystem;

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

        _ui.TryOpenUi(comp.BuiHolder, HereticLockShapeshiftUiKey.Key, uid);
    }

    private void OnLockShapeshiftSelect(EntityUid holderUid, HereticKnowledgeHolderComponent holderComp, HereticLockShapeshiftSelectMessage args)
    {
        var uid = args.Actor;
        if (!TryComp<HereticComponent>(uid, out var comp))
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
        var tearUid = Spawn("HereticLockTear", coords);
        var tearComp = EnsureComp<HereticLockTearComponent>(tearUid);
        tearComp.Master = uid;
        _actions.AddAction(uid, LockShapeshiftProto);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/ascend_knock.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-ascension-lock"), uid, uid, PopupType.Large);
    }
}

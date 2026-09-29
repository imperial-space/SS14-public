using System.Linq;
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
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
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
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Imperial.Lavaland.ColossusLoot;
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
using Content.Shared.Pulling.Events;
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
using Robust.Shared.Collections;
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
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;
using NewStatusEffectsSystem = Content.Shared.StatusEffectNew.StatusEffectsSystem;

namespace Content.Server.Imperial.Heretic.Paths.Flesh;

/// <summary>
/// Способности пути Плоти.
/// </summary>
public sealed class HereticFleshActionsSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem           _popup   = default!;
    [Dependency] private readonly SharedActionsSystem    _actions      = default!;
    [Dependency] private readonly IChatManager                _chatManager   = default!;

    private static readonly EntProtoId ShedHumanFormProto = "ActionHereticShedHumanForm";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HereticVoicelessDeadComponent, MobStateChangedEvent>(OnVoicelessDeadDied);
        SubscribeLocalEvent<HereticVoicelessDeadComponent, PlayerAttachedEvent>(OnVoicelessDeadPlayerAttached);
        SubscribeLocalEvent<HereticPathAscendedEvent>(OnPathAscended);
    }

    // ─── Flesh familiars ─────────────────────────────────────────────────────

    private void OnVoicelessDeadDied(EntityUid uid, HereticVoicelessDeadComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead) return;

        Spawn("HereticLivingHeart", Transform(uid).Coordinates);
    }

    private void OnVoicelessDeadPlayerAttached(EntityUid uid, HereticVoicelessDeadComponent comp, PlayerAttachedEvent args)
    {
        var rawMsg = Loc.GetString("heretic-voiceless-dead-chat");
        var wrapped = Loc.GetString("chat-manager-server-wrap-message", ("message", rawMsg));
        _chatManager.ChatMessageToOne(ChatChannel.Server, rawMsg, wrapped, default, false, args.Player.Channel);
    }

    private void OnPathAscended(ref HereticPathAscendedEvent args)
    {
        if (args.Component.CurrentPath != HereticPath.Flesh)
            return;

        var uid = args.Heretic;

        _actions.AddAction(uid, ShedHumanFormProto);
        _popup.PopupEntity(Loc.GetString("heretic-ascension-flesh"), uid, uid, PopupType.Large);
    }
}

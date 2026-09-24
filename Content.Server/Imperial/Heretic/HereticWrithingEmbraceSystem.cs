using Content.Server.Chat.Managers;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Robust.Shared.Prototypes;
using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticWrithingEmbraceSystem : EntitySystem
{
    private static readonly EntProtoId SenseToggleProto = "ActionHereticWrithingSense";

    [Dependency] private readonly SharedActionsSystem  _actions    = default!;
    [Dependency] private readonly IChatManager         _chatManager = default!;
    [Dependency] private readonly DamageableSystem     _damageable = default!;
    [Dependency] private readonly EntityLookupSystem   _lookup     = default!;
    [Dependency] private readonly IGameTiming          _timing     = default!;
    [Dependency] private readonly MobStateSystem       _mobState   = default!;
    [Dependency] private readonly MobThresholdSystem   _mobThreshold = default!;
    [Dependency] private readonly IPlayerManager       _playerManager = default!;
    [Dependency] private readonly SharedPopupSystem    _popup      = default!;

    private static readonly DamageSpecifier CurseDamage;
    private static readonly DamageSpecifier HealAmount;

    static HereticWrithingEmbraceSystem()
    {
        CurseDamage = new DamageSpecifier();
        CurseDamage.DamageDict["Slash"] = FixedPoint2.New(40);

        HealAmount = new DamageSpecifier();
        HealAmount.DamageDict["Blunt"]    = FixedPoint2.New(-5);
        HealAmount.DamageDict["Slash"]    = FixedPoint2.New(-5);
        HealAmount.DamageDict["Piercing"] = FixedPoint2.New(-5);
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticWrithingEmbraceComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<HereticWrithingEmbraceComponent, ClothingGotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<HereticComponent, HereticWrithingSenseToggleActionEvent>(OnSenseToggle);
    }

    private void OnEquipped(EntityUid uid, HereticWrithingEmbraceComponent comp, ClothingGotEquippedEvent args)
    {
        comp.Wearer = args.Wearer;

        if (!HasComp<HereticComponent>(args.Wearer))
        {
            _damageable.TryChangeDamage(args.Wearer, CurseDamage, true);
            _popup.PopupEntity(Loc.GetString("heretic-writhing-embrace-curse"), args.Wearer, args.Wearer, PopupType.LargeCaution);
            return;
        }

        _actions.AddAction(args.Wearer, ref comp.SenseToggleAction, SenseToggleProto);
    }

    private void OnUnequipped(EntityUid uid, HereticWrithingEmbraceComponent comp, ClothingGotUnequippedEvent args)
    {
        if (comp.Wearer is { } wearer && comp.SenseToggleAction is { } toggleAction)
            _actions.RemoveAction(wearer, toggleAction);

        comp.Wearer = null;
        comp.SenseToggleAction = null;
        comp.SilenceNotifications = false;
    }

    private void OnSenseToggle(EntityUid uid, HereticComponent hc, HereticWrithingSenseToggleActionEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;

        var query = EntityQueryEnumerator<HereticWrithingEmbraceComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            if (comp.Wearer != uid) continue;
            comp.SilenceNotifications = !comp.SilenceNotifications;
            var msgKey = comp.SilenceNotifications
                ? "heretic-writhing-embrace-sense-disabled"
                : "heretic-writhing-embrace-sense-enabled";
            SendServerChat(uid, Loc.GetString(msgKey));
            break;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<HereticWrithingEmbraceComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            if (comp.Wearer is not { } wearer) continue;
            var wearerXform = Transform(wearer);

            if (curTime >= comp.NextAuraTime)
            {
                comp.NextAuraTime = curTime + comp.AuraInterval;
                HealSummonedCreatures(comp, wearer, wearerXform);
            }

            if (!comp.SilenceNotifications && curTime >= comp.NextSenseTime)
            {
                comp.NextSenseTime = curTime + comp.SenseInterval;
                SenseNearbyHealth(comp, wearer, wearerXform);
            }
        }
    }

    private void HealSummonedCreatures(HereticWrithingEmbraceComponent comp, EntityUid wearer, TransformComponent wearerXform)
    {
        var coords = wearerXform.Coordinates;
        foreach (var target in _lookup.GetEntitiesInRange<MobStateComponent>(coords, comp.AuraRadius))
        {
            if (TryComp<HereticGhoulComponent>(target.Owner, out var ghoul) && ghoul.Master == wearer)
                _damageable.TryChangeDamage(target.Owner, HealAmount, ignoreResistances: true);
            else if (TryComp<HereticVoicelessDeadComponent>(target.Owner, out var dead) && dead.Master == wearer)
                _damageable.TryChangeDamage(target.Owner, HealAmount, ignoreResistances: true);
            else if (TryComp<HereticRawProphetComponent>(target.Owner, out var rawProphet) && rawProphet.Master == wearer)
                _damageable.TryChangeDamage(target.Owner, HealAmount, ignoreResistances: true);
        }
    }

    private void SendServerChat(EntityUid uid, string message)
    {
        if (!_playerManager.TryGetSessionByEntity(uid, out var session))
            return;
        var wrapped = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
        _chatManager.ChatMessageToOne(ChatChannel.Server, message, wrapped, default, false, session.Channel);
    }

    private void SenseNearbyHealth(HereticWrithingEmbraceComponent comp, EntityUid wearer, TransformComponent wearerXform)
    {
        var coords = wearerXform.Coordinates;
        foreach (var target in _lookup.GetEntitiesInRange<MobStateComponent>(coords, comp.SenseRadius))
        {
            if (target.Owner == wearer) continue;

            var name = MetaData(target.Owner).EntityName;

            if (_mobState.IsDead(target.Owner))
            {
                SendServerChat(wearer, Loc.GetString("heretic-writhing-embrace-sense-dead", ("name", name)));
                continue;
            }

            if (!_mobThreshold.TryGetThresholdForState(target.Owner, MobState.Dead, out var maxDmg))
                continue;

            var totalDmg = _damageable.GetTotalDamage(target.Owner);
            var currentHp = FixedPoint2.Max(maxDmg.Value - totalDmg, FixedPoint2.Zero);

            SendServerChat(wearer, Loc.GetString("heretic-writhing-embrace-sense-hp",
                ("name", name),
                ("current", (int) currentHp),
                ("max", (int) maxDmg.Value)));
        }
    }
}

using System.Linq;
using Content.Server.Actions;
using Content.Server.Administration;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Imperial.DayTime;
using Content.Server.Imperial.Medieval.GameTicking.Rules;
using Content.Server.Jittering;
using Content.Server.Mind;
using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Server.Stunnable;
using Content.Shared.Administration;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Imperial.Dash;
using Content.Shared.Imperial.Medieval.CCVar;
using Content.Shared.Imperial.Medieval.Lycantropy;
using Content.Shared.Imperial.Medieval.Plague;
using Content.Shared.Imperial.Medieval.Weapons;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Rejuvenate;
using Content.Shared.StatusEffect;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Microsoft.EntityFrameworkCore.Storage;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Lycantropy;

public sealed partial class LycantropySystem : SharedLycantropySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly BloodstreamSystem _blood = default!;
    [Dependency] private readonly JitteringSystem _jitter = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly InnerWeaponSystem _inner = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IConsoleHost _console = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly StunSystem _stun = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly RoleSystem _role = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;

    private const int PointsPerNight = 3;
    private const int PointsPerInfect = 4;
    private const int PointsPerCrit = 1;

    private DamageSpecifier _regenAmount = new()
    {
        DamageDict = new()
        {
            { "Brute", -1.5f },
            { "Slash", -1.5f },
            { "Piercing", -1.5f },
            { "Burn", -1.5f },
        }
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LycantropyComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<LycantropyComponent, PolymorphWerewolfActionEvent>(OnWerewolfPolymorph);

        SubscribeLocalEvent<LycantropyInfectedComponent, MobStateChangedEvent>(OnInfectedMobStateChanged);

        SubscribeLocalEvent<WerewolfComponent, MapInitEvent>(OnWerewolfMapInit);
        SubscribeLocalEvent<WerewolfComponent, WerewolfHowlActionEvent>(OnWerewolfHowl);
        SubscribeLocalEvent<WerewolfComponent, ToggleLycantropyInfectActionEvent>(OnWerewolfInfect);
        SubscribeLocalEvent<WerewolfComponent, MeleeHitEvent>(OnWerewolfHit);
        SubscribeLocalEvent<WerewolfComponent, MeleeDamageDealtEvent>(OnWerewolfDamageDealt);
        SubscribeLocalEvent<WerewolfComponent, WerewolfHealAlliesActionEvent>(OnHealAllies);
        SubscribeLocalEvent<WerewolfComponent, WerewolfRegenActionEvent>(OnRegen);
        SubscribeLocalEvent<WerewolfComponent, WerewolfStatusEffectActionEvent>(OnStatusEffect);
        SubscribeLocalEvent<WerewolfComponent, WerewolfTearingActionEvent>(OnTearing);
        SubscribeLocalEvent<WerewolfComponent, WerewolfShadowDashActionEvent>(OnShadowDash);
        SubscribeLocalEvent<WerewolfComponent, WerewolfBloodFeelActionEvent>(OnBloodFeel);

        SubscribeLocalEvent<WerewolfComponent, SetWerewolfDamageEvent>(OnSetDamage);
        SubscribeLocalEvent<WerewolfComponent, SetWerewolfDashStrengthEvent>(OnSetDashStrength);
        SubscribeLocalEvent<WerewolfComponent, SetWerewolfMobThresholdsEvent>(OnSetThresholds);


        SubscribeNetworkEvent<SelectWerewolfFormEvent>(OnSelectForm);
        SubscribeNetworkEvent<BuyLycantropyAbilityEvent>(OnBuyAbility);

        _console.RegisterCommand("setlycantropynight",
            Loc.GetString("cmd-weather-desc"),
            Loc.GetString("cmd-weather-help"),
            SetNightCommand,
            CommandCompletion);
    }

    [AdminCommand(AdminFlags.Fun)]
    private void SetNightCommand(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteError(Loc.GetString("cmd-weather-error-no-arguments"));
            return;
        }

        if (!bool.TryParse(args[0], out var night))
            return;

        if (night)
            OnNightStarted();
        else
            OnNightEnded();
    }

    private CompletionResult CommandCompletion(IConsoleShell shell, string[] args)
    {

        return CompletionResult.FromHintOptions(new List<string>() { "true", "false" }, Loc.GetString("night state"));
    }

    private void OnMapInit(EntityUid uid, LycantropyComponent comp, MapInitEvent args)
    {
        EntityUid? formEntity = null;
        if (_actions.AddAction(uid, ref formEntity, "SelectWerewolfFormAction"))
            comp.Actions.Add("Form", formEntity.Value);

        EntityUid? shopEntity = null;
        if (_actions.AddAction(uid, ref shopEntity, "LycantropyProgressMenuAction"))
            comp.Actions.Add("Menu", shopEntity.Value);
    }

    private void OnWerewolfPolymorph(EntityUid uid, LycantropyComponent comp, PolymorphWerewolfActionEvent args)
    {
        if (comp.Points < 2)
        {
            _popup.PopupEntity(Loc.GetString("popup-werewolf-polymorph-fail-cost"), uid, uid);
            return;
        }

        Morph(uid, comp);
    }

    private void OnInfectedMobStateChanged(EntityUid uid, LycantropyInfectedComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
        {
            if (TryComp<PolymorphedEntityComponent>(comp.Werewolf, out var polymorphed) && TryComp<LycantropyComponent>(polymorphed.Parent, out var lycantropy))
            {
                lycantropy.Points += PointsPerInfect;
                Dirty(polymorphed.Parent.Value, lycantropy);
            }

            if (_mind.TryGetMind(uid, out var mindId, out var mind))
                _role.MindAddRole(mindId, "MindRoleWerewolf");

            RemComp(uid, comp);

            if (MedievalLycantropyRuleSystem.IsBloodMoon)
            {
                var ev = new RejuvenateEvent();
                RaiseLocalEvent(uid, ev);
                Morph(uid, EnsureComp<LycantropyComponent>(uid));
            }
        }
        else if (args.NewMobState == MobState.Dead)
        {
            RemComp(uid, comp);
            RemComp<LycantropyComponent>(uid);
        }
    }

    private void OnWerewolfMapInit(EntityUid uid, WerewolfComponent comp, MapInitEvent args)
    {
        _actions.AddAction(uid, ref comp.InfectAction, "WerewolfInfectAction");
        _actions.AddAction(uid, "LycantropyHowlAction");
    }

    private void OnWerewolfHowl(EntityUid uid, WerewolfComponent comp, WerewolfHowlActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        _status.TryAddStatusEffect<WerewolfRegenComponent>(uid, "MedievalWerewolfRegen", TimeSpan.FromSeconds(20), true);

        _chat.TryEmoteWithChat(uid, "Howl", ChatTransmitRange.Normal, true);
        var xform = Transform(uid);
        var filter = Filter.BroadcastMap(xform.MapID).RemoveInRange(xform.MapPosition, 16f, EntityManager);
        _audio.PlayGlobal(_audio.ResolveSound(new SoundPathSpecifier("/Audio/Imperial/Medieval/Werewolf/howl-far.ogg")), filter, false);
    }

    private void OnWerewolfInfect(EntityUid uid, WerewolfComponent comp, ToggleLycantropyInfectActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _audio.PlayGlobal(args.Sound, uid);
        comp.InfectOn = !comp.InfectOn;
        _actions.SetToggled(comp.InfectAction, comp.InfectOn);
    }

    private void OnWerewolfHit(EntityUid uid, WerewolfComponent comp, MeleeHitEvent args)
    {
        if (!comp.InfectOn)
            return;

        if (_inner.TryGetInnerWeapon(uid, out var _, out var id) && id == "tearing_weapon")
        {
            _inner.SetWeapon(uid, "");
            _actions.SetToggled(comp.Actions["WerewolfTearingAction"], false);
        }

        if (!comp.InfectOn)
            return;

        comp.InfectOn = false;
        _actions.SetToggled(comp.InfectAction, comp.InfectOn);

        foreach (var item in args.HitEntities)
        {
            if (!HasComp<HumanoidAppearanceComponent>(item))
                continue;

            if (!_mobState.IsCritical(item))
                continue;

            _blood.TryModifyBleedAmount(item, -10);
            _jitter.DoJitter(item, TimeSpan.FromSeconds(3), true);
            EnsureComp<LycantropyComponent>(item);
            var infect = EnsureComp<LycantropyInfectedComponent>(item);
            infect.Werewolf = uid;
        }
    }

    private void OnWerewolfDamageDealt(EntityUid uid, WerewolfComponent comp, MeleeDamageDealtEvent args)
    {
        if (!_mobState.IsCritical(args.Target))
            return;

        if (comp.Critted.ContainsKey(args.Target))
            return;

        comp.Critted.Add(args.Target, _timing.CurTime + TimeSpan.FromSeconds(60));
        if (TryComp<PolymorphedEntityComponent>(uid, out var polymorphed) && TryComp<LycantropyComponent>(polymorphed.Parent, out var lycantropy))
        {
            lycantropy.Points += PointsPerCrit;
            Dirty(polymorphed.Parent.Value, lycantropy);
        }
    }

    private void OnHealAllies(EntityUid uid, WerewolfComponent comp, WerewolfHealAlliesActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        _audio.PlayPvs(args.Sound, uid);

        foreach (var item in _lookup.GetEntitiesInRange<WerewolfComponent>(Transform(uid).Coordinates, 5.5f))
        {
            if (item.Owner == uid)
                continue;

            _status.TryAddStatusEffect<WerewolfRegenComponent>(item.Owner, "MedievalWerewolfRegen", TimeSpan.FromSeconds(20), true);
        }
    }

    private void OnRegen(EntityUid uid, WerewolfComponent comp, WerewolfRegenActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var damage = new DamageSpecifier()
        {
            DamageDict = new()
            {
            { "Brute", -20f },
            { "Slash", -20f },
            { "Piercing", -20f },
            { "Burn", -15f },
            }
        };

        _audio.PlayPvs(args.Sound, uid);
        _damage.TryChangeDamage(uid, damage, true);
        _jitter.DoJitter(uid, TimeSpan.FromSeconds(3), true);
    }

    private void OnStatusEffect(EntityUid uid, WerewolfComponent comp, WerewolfStatusEffectActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        _status.TryAddStatusEffect(uid, args.Key, TimeSpan.FromSeconds(args.Time), args.Refresh, args.Component);

        if (args.Global)
            _audio.PlayGlobal(args.Sound, uid);
        else
            _audio.PlayPvs(args.Sound, uid);
    }

    private void OnTearing(EntityUid uid, WerewolfComponent comp, WerewolfTearingActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (_inner.TryGetInnerWeapon(uid, out var _, out var id) && id == "tearing_weapon")
        {
            _inner.SetWeapon(uid, "");
            _actions.SetToggled(args.Action.Owner, false);
        }
        else if (_inner.TryGetInnerWeapon(uid, out _, out _))
            return;
        else
        {
            _audio.PlayGlobal(args.Sound, uid);
            _inner.SetWeapon(uid, "tearing_weapon");
            _actions.SetToggled(args.Action.Owner, true);
        }
    }

    private void OnShadowDash(EntityUid uid, WerewolfComponent comp, WerewolfShadowDashActionEvent args)
    {
        if (args.Handled || !TryComp<MedievalDashComponent>(uid, out var dash))
            return;

        args.Handled = true;

        EnsureComp<WerewolfShadowDashComponent>(uid);
    }

    private void OnBloodFeel(EntityUid uid, WerewolfComponent comp, WerewolfBloodFeelActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        _appearance.SetData(uid, WerewolfBloodFeelVisuals.Active, !HasComp<WerewolfBloodFeelComponent>(uid));

        if (HasComp<WerewolfBloodFeelComponent>(uid))
            RemComp<WerewolfBloodFeelComponent>(uid);
        else
            EnsureComp<WerewolfBloodFeelComponent>(uid);
    }

    private void OnSetDamage(EntityUid uid, WerewolfComponent comp, SetWerewolfDamageEvent args)
    {
        if (!TryComp<MeleeWeaponComponent>(uid, out var weapon))
            return;

        foreach (var item in args.Replacements)
        {
            if (weapon.Damage.DamageDict.ContainsKey(item.Key))
                weapon.Damage.DamageDict[item.Key] = item.Value;
            else
                weapon.Damage.DamageDict.Add(item.Key, item.Value);
        }

        Dirty(uid, weapon);
    }

    private void OnSetDashStrength(EntityUid uid, WerewolfComponent comp, SetWerewolfDashStrengthEvent args)
    {
        if (!TryComp<MedievalDashComponent>(uid, out var dash))
            return;

        dash.Force *= args.Modifier;
        Dirty(uid, dash);
    }

    private void OnSetThresholds(EntityUid uid, WerewolfComponent comp, SetWerewolfMobThresholdsEvent args)
    {
        _mobThreshold.SetMobStateThreshold(uid, args.Death, Shared.Mobs.MobState.Dead);
        _mobThreshold.SetMobStateThreshold(uid, args.Crit, Shared.Mobs.MobState.Critical);
    }

    private void OnSelectForm(SelectWerewolfFormEvent args)
    {
        var uid = GetEntity(args.Ent);
        if (!TryComp<LycantropyComponent>(uid, out var comp))
            return;

        comp.SelectedForm = args.Proto;

        if (comp.Actions.TryGetValue("Form", out var ent))
            _actions.RemoveAction(uid, ent);
    }

    private void OnBuyAbility(BuyLycantropyAbilityEvent args)
    {
        var uid = GetEntity(args.Ent);
        if (!TryComp<LycantropyComponent>(uid, out var comp))
            return;

        var proto = _proto.Index(args.Proto);

        if (comp.Points < proto.Cost)
            return;

        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/Medieval/Werewolf/skill.ogg"), uid);

        comp.Points -= proto.Cost;
        comp.Abilities.Add(args.Proto);
        Dirty(uid, comp);

        foreach (var item in proto.HumanActions)
            _actions.AddAction(uid, item);
    }


    public void OnNightStarted()
    {
        var ents = EntityManager.AllEntities<LycantropyComponent>();
        _random.Shuffle(ents);

        var count = GetWerewolfTransformCount(ents.Count());
        for (var i = 0; i < ents.Count(); i++)
        {
            var entity = ents[i];

            if (i >= count && entity.Comp.NightsSpent < 2)
            {
                entity.Comp.NightsSpent++;
                continue;
            }

            Morph(entity.Owner, entity.Comp);
        }
    }

    public void OnNightEnded()
    {
        var ents = EntityManager.AllEntities<WerewolfComponent>();
        foreach (var item in ents)
        {
            item.Comp.RevertTime = _timing.CurTime + TimeSpan.FromSeconds(30);
            item.Comp.NextRevertPopup = _timing.CurTime;
            _jitter.DoJitter(item.Owner, TimeSpan.FromSeconds(30), true);
        }
    }

    private void Morph(EntityUid uid, LycantropyComponent comp)
    {
        var abilities = comp.Abilities;
        _audio.PlayGlobal(new SoundCollectionSpecifier("WerewolfTransform"), uid);
        _stun.TryAddParalyzeDuration(uid, TimeSpan.FromSeconds(3));
        _jitter.DoJitter(uid, TimeSpan.FromSeconds(3), true);

        Timer.Spawn(TimeSpan.FromSeconds(3), () =>
        {
            var mob = _polymorph.PolymorphEntity(uid, comp.SelectedForm ?? comp.AllowedPolymorphs.First().Value);

            if (!mob.HasValue)
                return;

            foreach (var item in abilities)
            {
                var proto = _proto.Index(item);

                if (proto.Event != null)
                    RaiseLocalEvent(mob.Value, proto.Event);

                foreach (var action in proto.WerewolfActions)
                {
                    EntityUid? ent = null;
                    _actions.AddAction(mob.Value, ref ent, action);
                    if (!ent.HasValue || !TryComp<WerewolfComponent>(mob, out var werewolf))
                        continue;

                    werewolf.Actions.Add(action, ent.Value);
                }
            }
        });
    }

    private float GetWerewolfTransformCount(int count)
    {
        if (MedievalLycantropyRuleSystem.IsBloodMoon)
            return count;

        if (count <= 2 && count > 0)
            return Math.Clamp(count - 1, 1, 2);

        return count / 2;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<WerewolfRegenComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.NextUpdate > _timing.CurTime)
                continue;

            comp.NextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);
            _damage.TryChangeDamage(uid, _regenAmount, true);
        }

        var wolfQuery = EntityQueryEnumerator<WerewolfComponent>();
        while (wolfQuery.MoveNext(out var uid, out var comp))
        {
            foreach (var item in new Dictionary<EntityUid, TimeSpan>(comp.Critted))
            {
                if (item.Value <= _timing.CurTime)
                    comp.Critted.Remove(item.Key);
            }

            if (comp.RevertTime.HasValue && comp.RevertTime <= _timing.CurTime)
            {
                comp.RevertTime = null;
                var ent = _polymorph.Revert(uid);
                if (TryComp<LycantropyComponent>(ent, out var lycantropy) && _mobState.IsAlive(ent.Value))
                {
                    lycantropy.Points += PointsPerNight;
                    Dirty(ent.Value, lycantropy);
                }

                continue;
            }

            if (comp.NextRevertPopup.HasValue && comp.NextRevertPopup <= _timing.CurTime)
            {
                comp.NextRevertPopup = _timing.CurTime + TimeSpan.FromSeconds(10);
                _popup.PopupEntity(Loc.GetString("popup-werewolf-revert-soon"), uid, uid);
            }
        }

        var infectedQuery = EntityQueryEnumerator<LycantropyInfectedComponent>();
        while (infectedQuery.MoveNext(out var uid, out var comp))
        {
            _damage.TryChangeDamage(uid, _regenAmount);
            _blood.TryModifyBleedAmount(uid, -1);
        }
    }
}

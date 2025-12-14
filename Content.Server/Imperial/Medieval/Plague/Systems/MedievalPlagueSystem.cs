using System.Linq;
using Content.Server.Actions;
using Content.Server.BadSmell.Components;
using Content.Server.Body.Systems;
using Content.Server.Buckle.Systems;
using Content.Server.Chat.Managers;
using Content.Server.DoAfter;
using Content.Server.Drunk;
using Content.Server.Fluids.EntitySystems;
using Content.Server.GameTicking.Events;
using Content.Server.Imperial.Medieval.Skills;
using Content.Server.Jittering;
using Content.Server.Medical;
using Content.Server.Polymorph.Systems;
using Content.Server.Popups;
using Content.Server.Stunnable;
using Content.Shared.ActionBlocker;
using Content.Shared.Alert;
using Content.Shared.Bed.Sleep;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Imperial.Medieval.Plague;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.StatusEffect;
using Content.Shared.Tag;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Toolshed.Commands.Values;

namespace Content.Server.Imperial.Medieval.Plague;

public sealed partial class MedievalPlagueSystem : SharedMedievalPlagueSystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly VomitSystem _vomit = default!;
    [Dependency] private readonly PuddleSystem _puddle = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly StunSystem _stun = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly BlindableSystem _blind = default!;
    [Dependency] private readonly JitteringSystem _jitter = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly BloodstreamSystem _blood = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _moveSpeed = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly DrunkSystem _drunk = default!;

    private Dictionary<ProtoId<MedievalPlagueSymptomPrototype>, MedievalPlagueSymptomData> _symptoms = new();
    private int _strapHealResistance = 0;
    private float _healItemMod = 1f;

    private const float PointsUpdateInterval = 600f;
    private TimeSpan _nextPointsUpdate = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);

        InitializeGhost();
        InitializeSpread();
        InitializeUi();
        InitializeSymptoms();
    }

    private void OnRoundStarting(RoundStartingEvent args)
    {
        _symptoms.Clear();
        _strapHealResistance = 0;
        _healItemMod = 1f;
        _spreaders.Clear();
        _contactSpreadChance = 0f;
        _blockersEfficiency = 1f;
        _minSmellLevel = 50f;
        _allergyRandom = new();
        CurrentCure = "MedievalPlagueCure4";

        _bloodlettingProbabilities = new()
        {
            {
                BloodlettingResult.Healthy, new()
                {
                    { BloodlettingResult.Healthy, 0.95f },
                    { BloodlettingResult.Infected, 0.03f },
                    { BloodlettingResult.Immune, 0.02f }
                }
            },
            {
                BloodlettingResult.Immune, new()
                {
                    { BloodlettingResult.Immune, 0.95f },
                    { BloodlettingResult.Healthy, 0.04f },
                    { BloodlettingResult.Infected, 0.01f }
                }
            },
            {
                BloodlettingResult.InfectedIncub, new()
                {
                    { BloodlettingResult.Infected, 0.93f },
                    { BloodlettingResult.Healthy, 0.05f },
                    { BloodlettingResult.Immune, 0.03f }
                }
            },
            {
                BloodlettingResult.Infected, new()
                {
                    { BloodlettingResult.Infected, 0.97f },
                    { BloodlettingResult.Healthy, 0.03f }
                }
            },
        };
    }

    public bool TryInfect(EntityUid uid, float additionalMod = 1f, bool addPoint = true)
    {
        if (!HasComp<MedievalCanBeInfectedComponent>(uid) || HasComp<MedievalPlagueInfectedComponent>(uid))
            return false;

        if (HasComp<MedievalPlagueImmuneComponent>(uid))
            return false;

        var ev = new MedievalPlagueInfectionAttemptEvent();
        RaiseLocalEvent(uid, ref ev);

        if (!_random.Prob(ev.Probability * additionalMod))
            return false;

        Infect(uid, addPoint);
        return true;
    }

    public void Infect(EntityUid uid, bool addPoint = true)
    {
        EnsureComp<MedievalPlagueInfectedComponent>(uid);

        _data.Infected++;

        if (addPoint)
            AddPoints();

        RaisePrototypeIncubationEvents(uid);
    }

    public bool CanInfectionProgress(EntityUid uid, MedievalPlagueInfectedComponent? comp)
    {
        if (!Resolve(uid, ref comp))
            return false;

        if (TryComp<BadSmellComponent>(uid, out var smell) && smell.SmellLevel < _minSmellLevel && comp.Incubation)
            return false;

        return true;
    }

    public void TryProgressInfection(EntityUid uid, float progress, MedievalPlagueInfectedComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        if (progress > 0 && !CanInfectionProgress(uid, comp))
            return;

        comp.Progression += progress;
        if (comp.Progression <= 0)
        {
            RemComp(uid, comp);

            _data.Infected--;
            _data.Immune++;

            var immune = EnsureComp<MedievalPlagueImmuneComponent>(uid);
            immune.StartTime = _timing.CurTime;
        }

        else if (comp.Incubation != comp.Progression < 50)
        {
            comp.Incubation = comp.Progression < 50;
            Dirty(uid, comp);

            if (comp.Incubation)
            {
                comp.PlagueComponents.ForEach(x => RemComp(uid, x));
                comp.Effects.Clear();
                RaisePrototypeIncubationEvents(uid);
            }
            else
            {
                comp.IncubationComponents.ForEach(x => RemComp(uid, x));
                comp.IncubationEffects.Clear();
                RaisePrototypeEvents(uid);
            }
        }

    }

    private void DoPrototypeEffects(ProtoId<MedievalPlagueSymptomPrototype> protoId)
    {
        var infected = EntityManager.AllEntities<MedievalPlagueInfectedComponent>();
        var ghosts = EntityManager.AllEntities<MedievalPlagueGhostComponent>();

        foreach (var target in infected)
        {
            if (target.Comp.Incubation)
                RaisePrototypeIncubationEvent(target, protoId);
            else
                RaisePrototypeEvent(target, protoId);
        }

        foreach (var ghost in ghosts)
        {
            AddPrototypeActions(ghost, protoId);
        }

        var ev = _proto.Index(protoId).BroadcastEvent;

        if (ev != null)
            RaiseLocalEvent(ev);
    }

    private void RaisePrototypeEvent(EntityUid uid, ProtoId<MedievalPlagueSymptomPrototype> protoId)
    {
        var proto = _proto.Index(protoId);

        if (proto.TargetEvent != null)
            RaiseLocalEvent(uid, proto.TargetEvent);
    }

    private void RaisePrototypeIncubationEvent(EntityUid uid, ProtoId<MedievalPlagueSymptomPrototype> protoId)
    {
        var proto = _proto.Index(protoId);

        if (proto.IncubationTargetEvent != null)
            RaiseLocalEvent(uid, proto.IncubationTargetEvent);
    }

    private void RaisePrototypeEvents(EntityUid uid)
    {
        foreach (var item in _symptoms.Where(x => x.Value.Unlocked))
        {
            var proto = _proto.Index(item.Key);

            if (proto.TargetEvent != null)
                RaiseLocalEvent(uid, proto.TargetEvent);
        }
    }

    private void RaisePrototypeIncubationEvents(EntityUid uid)
    {
        foreach (var item in _symptoms.Where(x => x.Value.Unlocked))
        {
            var proto = _proto.Index(item.Key);

            if (proto.TargetEvent != null)
                RaiseLocalEvent(uid, proto.TargetEvent);
        }
    }

    private void AddPrototypeActions(EntityUid uid, ProtoId<MedievalPlagueSymptomPrototype> protoId, MedievalPlagueGhostComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        var proto = _proto.Index(protoId);
        if (proto.Actions.Count() <= 0)
            return;

        foreach (var act in proto.Actions)
        {
            var actionEnt = _actions.AddAction(uid, act);
            if (actionEnt.HasValue)
                comp.Actions.Add(actionEnt.Value);
        }
    }

    private bool TryChangePoints(EntityUid uid, int points, MedievalPlagueGhostComponent? comp = null)
    {
        if (points == 0)
            return true;

        if (!Resolve(uid, ref comp))
            return false;

        if (comp.Points + points < 0)
            return false;

        comp.Points += points;
        _data.Points += points;
        Dirty(uid, comp);
        UpdateUi(uid);
        _alerts.ShowAlert(uid, comp.AlertId);

        return true;
    }

    private void AddPoints()
    {
        var ghosts = EntityManager.AllEntities<MedievalPlagueGhostComponent>();
        foreach (var item in ghosts)
        {
            item.Comp.Points++;
            _data.Points++;
            Dirty(item);
            _alerts.ShowAlert(item.Owner, item.Comp.AlertId);
        }

        UpdateUi();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateInfected();
        UpdateSickness();
        UpdateClumsiness();
        UpdateDamagingClothing();
        UpdateLungCancer();
        UpdatePoints();
    }

    private void UpdateInfected()
    {
        var query = EntityQueryEnumerator<MedievalPlagueInfectedComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.NextUpdate > _timing.CurTime)
                continue;

            comp.NextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);

            if (_mobState.IsDead(uid))
                continue;

            DoEffects(uid, comp);

            if (TryComp<BuckleComponent>(uid, out var buckle) &&
                TryComp<MedievalPlagueHealStrappedComponent>(buckle.BuckledTo, out var heal) &&
                heal.Level > _strapHealResistance)
            {
                TryProgressInfection(uid, -1, comp);
                continue;
            }

            if (comp.NextProgression > _timing.CurTime)
                continue;

            TryProgressInfection(uid, 1, comp);
            comp.NextProgression = _timing.CurTime + TimeSpan.FromSeconds(comp.UpdatePeriod);
        }
    }

    private void UpdatePoints()
    {
        if (_timing.CurTime < _nextPointsUpdate)
            return;
        _nextPointsUpdate = _timing.CurTime + TimeSpan.FromSeconds(PointsUpdateInterval);

        var infected = EntityManager.AllEntities<MedievalPlagueInfectedComponent>();
        var points = 0;

        for (var i = 0; i < infected.Count(); i++)
        {
            var item = infected[i];
            if (item.Comp.Incubation)
                points++;
            else
                points += 2;
        }

        points /= 8;
        var ghosts = EntityManager.AllEntities<MedievalPlagueGhostComponent>();
        foreach (var item in ghosts)
            TryChangePoints(item, points, item.Comp);
    }

    public override void GrantPlagueImmunity(EntityUid uid, string? cure)
    {
        if (HasComp<MedievalPlagueInfectedComponent>(uid) ||
           HasComp<MedievalPlagueImmuneComponent>(uid) ||
           !HasComp<MedievalCanBeInfectedComponent>(uid))
            return;

        var immune = EnsureComp<MedievalPlagueImmuneComponent>(uid);
        immune.StartTime = _timing.CurTime;
        immune.HardImmunity = true;
    }

    public override void TryProgressInfection(EntityUid uid, float amount, string? reagent)
    {
        if (reagent != null && reagent != CurrentCure)
            return;

        TryProgressInfection(uid, amount);
    }
}

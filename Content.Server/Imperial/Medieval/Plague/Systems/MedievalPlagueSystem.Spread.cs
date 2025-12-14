using System.Linq;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Imperial.Medieval.Plague;
using Content.Shared.Interaction;
using Content.Shared.Movement.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Plague;

public sealed partial class MedievalPlagueSystem
{
    private Dictionary<string, float> _spreaders = new();
    private float _contactSpreadChance = 0f;
    private float _blockersEfficiency = 1f;
    private float _minSmellLevel = 50f;

    public ProtoId<ReagentPrototype> CurrentCure = "MedievalPlagueCure4";

    private Dictionary<BloodlettingResult, Dictionary<BloodlettingResult, float>> _bloodlettingProbabilities = new()
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

    private void InitializeSpread()
    {
        SubscribeLocalEvent<MedievalPlagueInfectedComponent, StartCollideEvent>(OnInfectedCollide);
        SubscribeLocalEvent<MedievalPlagueInfectOnHitComponent, ComponentInit>(OnSpreaderInit);
        SubscribeLocalEvent<MedievalPlagueInfectOnHitComponent, MeleeHitEvent>(OnSpreaderHit);
        SubscribeLocalEvent<MedievalPlagueSpreadBlockingComponent, MedievalPlagueInfectionAttemptEvent>(OnBlockerInfectionAttempt);

        SubscribeLocalEvent<MedievalPlagueInfectedComponent, PlagueHealingItemUsedEvent>(OnHealingItemUsed);
        SubscribeLocalEvent<BloodlettingToolComponent, ExaminedEvent>(OnBloodlettingExamine);
        SubscribeLocalEvent<BloodlettingToolComponent, AfterInteractEvent>(OnBloodlettingUse);
        SubscribeLocalEvent<BloodlettingToolComponent, BloodlettingDoAfterEvent>(OnBloodlettingDoAfter);

        SubscribeLocalEvent<SetContactSpreadModifierEvent>(OnSetContactSpreadMod);
        SubscribeLocalEvent<SetSpreaderChanceEvent>(OnSetSpreaderChance);
        SubscribeLocalEvent<SetPlagueBlockerModifierEvent>(OnSetBlockerMod);
        SubscribeLocalEvent<SetStrapHealResistanceEvent>(OnSetStrapResistance);
        SubscribeLocalEvent<SetBloodlettingProbabilitiesEvent>(OnSetBloodlettingProb);
        SubscribeLocalEvent<SetPlagueCureEvent>(OnSetCure);
    }

    private void OnInfectedCollide(EntityUid uid, MedievalPlagueInfectedComponent comp, ref StartCollideEvent args)
    {
        if (!TryComp<MobCollisionComponent>(uid, out var collision) || args.OurFixtureId != collision.FixtureId)
            return;

        if (comp.NextCollideSpread > _timing.CurTime)
            return;

        comp.NextCollideSpread = _timing.CurTime + TimeSpan.FromSeconds(1);
        TryInfect(args.OtherEntity, _contactSpreadChance);
    }

    private void OnSpreaderInit(EntityUid uid, MedievalPlagueInfectOnHitComponent comp, ComponentInit args)
    {
        if (_spreaders.Keys.Contains(comp.Id))
            comp.Active = true;
    }

    private void OnSpreaderHit(EntityUid uid, MedievalPlagueInfectOnHitComponent comp, MeleeHitEvent args)
    {
        if (!comp.Active)
            return;

        var chance = comp.Chance * _spreaders.GetValueOrDefault(comp.Id, 1f);

        foreach (var item in args.HitEntities)
            TryInfect(item, chance);
    }

    private void OnBlockerInfectionAttempt(EntityUid uid, MedievalPlagueSpreadBlockingComponent comp, ref MedievalPlagueInfectionAttemptEvent args)
    {
        var mod = comp.Modifier + (1 - comp.Modifier) * (_blockersEfficiency - 1);

        args.Probability *= mod;
    }

    private void OnHealingItemUsed(EntityUid uid, MedievalPlagueInfectedComponent comp, ref PlagueHealingItemUsedEvent args)
    {
        TryProgressInfection(uid, -args.PlagueDecay * _healItemMod, comp);
    }

    private void OnBloodlettingExamine(EntityUid uid, BloodlettingToolComponent comp, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || comp.Result == BloodlettingResult.None)
            return;

        args.PushMarkup(Loc.GetString($"plague-bloodletting-tool-examine-{comp.Result.ToString().ToLower()}"), -1);
    }

    private void OnBloodlettingUse(EntityUid uid, BloodlettingToolComponent comp, AfterInteractEvent args)
    {
        if (!HasComp<MedievalPlagueInfectedComponent>(args.Target) &&
            !HasComp<MedievalCanBeInfectedComponent>(args.Target) &&
            !HasComp<MedievalPlagueImmuneComponent>(args.Target))
            return;

        if (comp.Result != BloodlettingResult.None || comp.DoAfter.HasValue)
            return;

        var ev = new DoAfterArgs(EntityManager, args.User, comp.Duration, new BloodlettingDoAfterEvent(), uid, args.Target, uid)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnDropItem = true,
            BreakOnHandChange = true
        };

        _doAfter.TryStartDoAfter(ev, out comp.DoAfter);
    }

    private void OnBloodlettingDoAfter(EntityUid uid, BloodlettingToolComponent comp, BloodlettingDoAfterEvent args)
    {
        comp.DoAfter = null;

        if (args.Cancelled || args.Target == null)
            return;

        if (!HasComp<MedievalPlagueInfectedComponent>(args.Target) &&
                !HasComp<MedievalCanBeInfectedComponent>(args.Target) &&
                !HasComp<MedievalPlagueImmuneComponent>(args.Target))
            return;

        if (comp.Result != BloodlettingResult.None)
            return;

        var realResult = BloodlettingResult.None;
        if (HasComp<MedievalPlagueImmuneComponent>(args.Target))
            realResult = BloodlettingResult.Immune;
        else if (TryComp<MedievalPlagueInfectedComponent>(args.Target, out var infected))
            realResult = infected.Incubation ? BloodlettingResult.InfectedIncub : BloodlettingResult.Infected;
        else
            realResult = BloodlettingResult.Healthy;

        var prob = _random.NextFloat(1f);
        var cach = 0f;
        var result = BloodlettingResult.None;

        for (var i = 0; i < _bloodlettingProbabilities[realResult].Count; i++)
        {
            var item = _bloodlettingProbabilities[realResult].ElementAt(i);
            if (prob > item.Value + cach)
            {
                cach += item.Value;
                continue;
            }

            result = item.Key;
        }

        _popup.PopupEntity(
                        Loc.GetString($"plague-bloodletting-result-{result.ToString().ToLower()}",
                                    ("target", Identity.Name(args.Target.Value, EntityManager, args.User))),
                        args.User, args.User, Shared.Popups.PopupType.Medium);
        _damageable.TryChangeDamage(args.Target, comp.Damage, true);

        comp.Result = result;
        _appearance.SetData(uid, BloodlettingVisuals.Data, (int)result);
    }

    private void OnSetContactSpreadMod(SetContactSpreadModifierEvent args)
        => _contactSpreadChance = args.Modifier;

    private void OnSetSpreaderChance(SetSpreaderChanceEvent args)
    {
        if (_spreaders.TryGetValue(args.Id, out _))
            _spreaders[args.Id] = args.Modifier;
        else
            _spreaders.Add(args.Id, args.Modifier);

        foreach (var item in EntityManager.AllEntities<MedievalPlagueInfectOnHitComponent>())
        {
            if (item.Comp.Id != args.Id)
                continue;

            item.Comp.Active = true;
        }
    }

    private void OnSetBlockerMod(SetPlagueBlockerModifierEvent args)
        => _blockersEfficiency = args.Modifier;

    private void OnSetStrapResistance(SetStrapHealResistanceEvent args)
    {
        _strapHealResistance = args.StrapResistance;
        _healItemMod = args.HealMod;
    }

    private void OnSetCure(SetPlagueCureEvent args)
    {
        CurrentCure = args.Reagent;
    }

    private void OnSetBloodlettingProb(SetBloodlettingProbabilitiesEvent args)
    {
        foreach (var item in args.Data)
        {
            _bloodlettingProbabilities[item.Key] = item.Value;
        }
    }
}

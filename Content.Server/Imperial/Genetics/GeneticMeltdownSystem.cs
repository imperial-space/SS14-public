using System.Linq;
using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.EntityEffects;
using Content.Shared.Gibbing;
using Content.Shared.Imperial.Genetics;
using Content.Shared.Imperial.Genetics.Components;
using Content.Shared.Imperial.Genetics.Prototypes;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Genetics;

/// <summary>
/// Срыв при распаде ДНК (something_horrible из SS13): все мутации снимаются, стабильность возвращается к 100,
/// и случается один из срывов. Шанс несмертельного — 70% минус то, насколько стабильность ушла ниже нуля.
/// </summary>
public sealed class GeneticMeltdownSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly GeneticsSystem _genetics = default!;
    [Dependency] private readonly GibbingSystem _gibbing = default!;
    [Dependency] private readonly SharedEntityEffectsSystem _effects = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    private static readonly EntProtoId AshProto = "Ash";
    private static readonly TimeSpan DecloningInterval = TimeSpan.FromSeconds(2);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GenomeComponent, GenomeMeltdownEvent>(OnMeltdown);
        SubscribeLocalEvent<GeneticFragileComponent, DamageModifyEvent>(OnFragileDamage);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<GeneticDecloningComponent>();
        while (query.MoveNext(out var uid, out var decloning))
        {
            if (now < decloning.NextDamage)
                continue;

            decloning.NextDamage = now + DecloningInterval;
            _damageable.TryChangeDamage(uid, decloning.Damage, true);
        }
    }

    private void OnMeltdown(Entity<GenomeComponent> ent, ref GenomeMeltdownEvent args)
    {
        if (ent.Comp.Stability > 0)
            return;

        _genetics.DeactivateAll(ent, keepNatural: false);
        ent.Comp.Stability = 100;
        ent.Comp.MeltdownAt = null;

        var nonFatal = _random.Prob(Math.Max(70 - args.Instability, 0) / 100f);
        var pool = _proto.EnumeratePrototypes<GeneticMeltdownPrototype>().Where(p => p.Fatal != nonFatal).ToList();
        if (pool.Count == 0)
            return;

        var meltdown = PickWeighted(pool);
        _adminLog.Add(LogType.Action, meltdown.Fatal ? LogImpact.High : LogImpact.Medium,
            $"{ToPrettyString(ent):player} suffered genetic meltdown {meltdown.ID}");
        Apply(ent, meltdown);
    }

    private GeneticMeltdownPrototype PickWeighted(List<GeneticMeltdownPrototype> pool)
    {
        var roll = _random.NextFloat() * pool.Sum(p => p.Weight);
        foreach (var proto in pool)
        {
            roll -= proto.Weight;
            if (roll <= 0)
                return proto;
        }

        return pool[^1];
    }

    public void Apply(EntityUid uid, GeneticMeltdownPrototype meltdown)
    {
        if (meltdown.Popup is { } popup)
            _popup.PopupEntity(Loc.GetString(popup), uid, uid, PopupType.LargeCaution);

        if (meltdown.Effects.Length > 0)
            _effects.ApplyEffects(uid, meltdown.Effects);

        switch (meltdown.Action)
        {
            case MeltdownAction.Monkey:
                _genetics.CompleteBlock(uid, GeneticsSystem.RaceMutation);
                _genetics.TryActivate(uid, GeneticsSystem.RaceMutation, MutationSource.Activated);
                break;
            case MeltdownAction.Fragile:
                EnsureComp<GeneticFragileComponent>(uid);
                break;
            case MeltdownAction.Decloning:
                EnsureComp<GeneticDecloningComponent>(uid);
                break;
            case MeltdownAction.Yeet:
                var direction = _random.NextAngle().ToVec();
                _throwing.TryThrow(uid, direction * 20f, 10f);
                break;
            case MeltdownAction.Paraplegic:
                EnsureComp<LegsParalyzedComponent>(uid);
                break;
            case MeltdownAction.Gib:
                _gibbing.Gib(uid);
                break;
            case MeltdownAction.Dust:
                Spawn(AshProto, Transform(uid).Coordinates);
                QueueDel(uid);
                break;
        }
    }

    private void OnFragileDamage(Entity<GeneticFragileComponent> ent, ref DamageModifyEvent args)
    {
        args.Damage *= ent.Comp.Multiplier;
    }
}

/// <summary>Срыв «всё в порядке… нет»: урон по носителю умножается (damage_resistance −20000).</summary>
[RegisterComponent]
public sealed partial class GeneticFragileComponent : Component
{
    [DataField]
    public float Multiplier = 200f;
}

/// <summary>Медленный клеточный распад после срыва. Лечится мутадоном.</summary>
[RegisterComponent]
public sealed partial class GeneticDecloningComponent : Component
{
    [DataField]
    public DamageSpecifier Damage = new() { DamageDict = { ["Cellular"] = 1 } };

    [ViewVariables]
    public TimeSpan NextDamage;
}

using Content.Shared.Stacks;
using Robust.Shared.Map.Components;
using Content.Shared.Random.Helpers;
using Content.Shared.Prototypes;
using Content.Shared.Coordinates.Helpers;
using Robust.Shared.Random;
using System.Linq;
using Content.Server.Imperial.Xenobiology.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Electrocution;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Slippery;

namespace Content.Server.Imperial.Xenobiology;

public sealed partial class SlimeCrossbreedSystem
{
    [Dependency] private readonly SharedGodmodeSystem _godmode = default!;

    private static readonly Color SlimeskinTint = Color.FromHex("#3070CC");
    private static readonly Color RainbowTint = Color.FromHex("#FF9CFF");
    private static readonly Color SpookTint = Color.FromHex("#E8E8E8");
    private static readonly Color CloneTint = Color.FromHex("#007BA7");

    private void InitializeEffects()
    {
        SubscribeLocalEvent<SlimeCrossEffectsComponent, DamageModifyEvent>(OnEffectsDamageModify);
        SubscribeLocalEvent<SlimeCrossEffectsComponent, ElectrocutionAttemptEvent>(OnEffectsElectrocution);
        SubscribeLocalEvent<SlimeCloneComponent, MobStateChangedEvent>(OnCloneState);
    }

    #region API

    /// <summary>apply_status_effect(): null — бессрочно (снимается вручную).</summary>
    public void AddEffect(EntityUid uid, string key, TimeSpan? duration)
    {
        var comp = EnsureComp<SlimeCrossEffectsComponent>(uid);
        var isNew = !comp.Effects.ContainsKey(key);
        comp.Effects[key] = duration is { } d ? _timing.CurTime + d : TimeSpan.MaxValue;
        if (isNew)
            OnEffectAdded(uid, comp, key);
    }

    public void RemoveEffect(EntityUid uid, string key)
    {
        if (!TryComp<SlimeCrossEffectsComponent>(uid, out var comp) || !comp.Effects.Remove(key))
            return;
        OnEffectRemoved(uid, comp, key);
    }

    public bool HasEffect(EntityUid uid, string key)
    {
        return TryComp<SlimeCrossEffectsComponent>(uid, out var comp) && comp.Effects.ContainsKey(key);
    }

    public void SetTint(EntityUid uid, string key, Color color)
    {
        var tint = EnsureComp<SlimeTintComponent>(uid);
        tint.Tints.Remove(key);
        tint.Tints[key] = color;
        Dirty(uid, tint);
    }

    public void ClearTint(EntityUid uid, string key)
    {
        if (!TryComp<SlimeTintComponent>(uid, out var tint) || !tint.Tints.Remove(key))
            return;
        if (tint.Tints.Count == 0)
            RemCompDeferred<SlimeTintComponent>(uid);
        else
            Dirty(uid, tint);
    }

    private void OnEffectAdded(EntityUid uid, SlimeCrossEffectsComponent comp, string key)
    {
        switch (key)
        {
            case "slimeskin":
                SetTint(uid, key, SlimeskinTint);
                break;
            case "rainbow":
                SetTint(uid, key, RainbowTint);
                if (!HasComp<GodmodeComponent>(uid))
                {
                    _godmode.EnableGodmode(uid);
                    comp.GaveGodmode = true;
                }
                UpdatePacified(uid, comp);
                break;
            case "plur":
            case "stab-lightpink":
                UpdatePacified(uid, comp);
                if (key == "stab-lightpink")
                    _speed.SetModifier(uid, key, 1.33f);
                break;
            case "water":
            case "stab-blue":
                if (!HasComp<NoSlipComponent>(uid))
                {
                    AddComp<NoSlipComponent>(uid);
                    comp.GaveNoSlip = true;
                }
                break;
            case "spook":
                SetTint(uid, key, SpookTint);
                break;
            case "tarfoot":
                _speed.SetModifier(uid, key, 0.8f);
                break;
            case "bloodchill":
                _speed.SetModifier(uid, key, 0.4f);
                break;
            case "golem":
                _speed.SetModifier(uid, key, 0.75f);
                SetTint(uid, key, Color.FromHex("#A0A0A0"));
                break;
        }
    }

    private void OnEffectRemoved(EntityUid uid, SlimeCrossEffectsComponent comp, string key)
    {
        switch (key)
        {
            case "slimeskin":
            case "spook":
                ClearTint(uid, key);
                if (key == "slimeskin")
                    Popup("slime-cross-slimeskin-off", uid, ("target", uid));
                break;
            case "rainbow":
                ClearTint(uid, key);
                if (comp.GaveGodmode && !comp.Effects.ContainsKey("rainbow"))
                {
                    _godmode.DisableGodmode(uid);
                    comp.GaveGodmode = false;
                }
                UpdatePacified(uid, comp);
                Popup("slime-cross-rainbow-off", uid, ("target", uid));
                break;
            case "plur":
            case "stab-lightpink":
                UpdatePacified(uid, comp);
                if (key == "stab-lightpink")
                    _speed.SetModifier(uid, key, 1f);
                break;
            case "water":
            case "stab-blue":
                if (comp.GaveNoSlip && !comp.Effects.ContainsKey("water") && !comp.Effects.ContainsKey("stab-blue"))
                {
                    RemComp<NoSlipComponent>(uid);
                    comp.GaveNoSlip = false;
                }
                break;
            case "tarfoot":
            case "bloodchill":
                _speed.SetModifier(uid, key, 1f);
                break;
            case "golem":
                _speed.SetModifier(uid, key, 1f);
                ClearTint(uid, key);
                break;
        }
    }

    /// <summary>TRAIT_PACIFISM от нескольких источников.</summary>
    private void UpdatePacified(EntityUid uid, SlimeCrossEffectsComponent comp)
    {
        var want = comp.Effects.ContainsKey("rainbow") || comp.Effects.ContainsKey("plur") || comp.Effects.ContainsKey("stab-lightpink");
        if (want && !HasComp<PacifiedComponent>(uid))
        {
            AddComp<PacifiedComponent>(uid);
            comp.GavePacified = true;
        }
        else if (!want && comp.GavePacified)
        {
            RemComp<PacifiedComponent>(uid);
            comp.GavePacified = false;
        }
    }

    #endregion

    #region Хуки

    /// <summary>physiology: brute_mod, burn_mod, damage_resistance, TRAIT_TOXINLOVER.</summary>
    private void OnEffectsDamageModify(Entity<SlimeCrossEffectsComponent> ent, ref DamageModifyEvent args)
    {
        var effects = ent.Comp.Effects;
        var all = 1f;
        if (effects.ContainsKey("slimeskin"))
            all -= 0.1f;
        if (effects.ContainsKey("stab-adamantine"))
            all -= 0.05f;

        var brute = all * (effects.ContainsKey("metal") ? 0.9f : 1f) * (effects.ContainsKey("golem") ? 0.55f : 1f);
        var burn = all * (effects.ContainsKey("adamantinecookie") ? 0.9f : 1f) * (effects.ContainsKey("golem") ? 0.75f : 1f);
        var toxinLover = effects.ContainsKey("toxinlover");
        if (MathHelper.CloseTo(brute, 1f) && MathHelper.CloseTo(burn, 1f) && MathHelper.CloseTo(all, 1f) && !toxinLover)
            return;

        var modified = new DamageSpecifier(args.Damage);
        foreach (var (type, amount) in args.Damage.DamageDict)
        {
            if (amount <= 0)
                continue;
            var mult = type.Id switch
            {
                "Blunt" or "Slash" or "Piercing" => brute,
                "Heat" or "Cold" or "Shock" or "Caustic" => burn,
                "Poison" or "Radiation" when toxinLover => -1f,
                _ => all,
            };
            modified.DamageDict[type] = amount * mult;
        }

        args.Damage = modified;
    }

    /// <summary>sparkcookie: siemens_coeff = 0.</summary>
    private void OnEffectsElectrocution(Entity<SlimeCrossEffectsComponent> ent, ref ElectrocutionAttemptEvent args)
    {
        if (ent.Comp.Effects.ContainsKey("spark"))
            args.Cancel();
    }

    #endregion

    #region Тик эффектов

    private void UpdateEffects(TimeSpan now)
    {
        var query = EntityQueryEnumerator<SlimeCrossEffectsComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            foreach (var (key, end) in comp.Effects.ToList())
            {
                if (now >= end)
                    RemoveEffect(uid, key);
            }

            if (now < comp.NextTick)
                continue;
            comp.NextTick = now + TimeSpan.FromSeconds(1);
            TickEffects(uid, comp);

            if (comp.Effects.Count == 0 && !comp.GavePacified && !comp.GaveGodmode && !comp.GaveNoSlip)
                RemCompDeferred<SlimeCrossEffectsComponent>(uid);
        }

        UpdateDejavu(now);
        UpdateClones(now);
    }

    private void TickEffects(EntityUid uid, SlimeCrossEffectsComponent comp)
    {
        var coords = At(uid);
        var effects = comp.Effects;

        if (effects.ContainsKey("water"))
        {
            foreach (var tile in TilesAround(coords.SnapToGrid(EntityManager), 1))
            {
                _puddle.TrySpillAt(tile, new Solution("Water", 1), out _, sound: false);
            }
        }

        if (effects.ContainsKey("love") && !_mobState.IsIncapacitated(uid))
        {
            var huggables = _lookup.GetEntitiesInRange<HumanoidProfileComponent>(coords, 1.5f).Where(h => h.Owner != uid).ToList();
            if (huggables.Count > 0)
                Popup("slime-cookie-hug", uid, ("user", uid), ("target", (object) _random.Pick(huggables).Owner));
        }

        if (effects.ContainsKey("tar"))
        {
            foreach (var mob in _lookup.GetEntitiesInRange<HumanoidProfileComponent>(coords, 1.5f))
            {
                if (mob.Owner != uid)
                    AddEffect(mob, "tarfoot", TimeSpan.FromSeconds(3));
            }
        }

        if (effects.ContainsKey("peace"))
        {
            foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 1.5f))
            {
                AddEffect(mob, "plur", TimeSpan.FromSeconds(3));
            }
        }

        // rebreathing / golem: TRAIT_NOBREATH.
        if (effects.ContainsKey("golem") || effects.ContainsKey("rebreathing"))
            Heal(uid, 0f, 0f, 0f, 6f);

        if (effects.ContainsKey("bloodchill") && _random.Prob(0.5f))
            Damage(uid, "Cold", 2f);
    }

    #endregion

    #region Дежавю

    /// <summary>component/dejavu: через 10 секунд существо возвращается вместе с прежним уроном.</summary>
    public void StartDejavu(EntityUid target, int rewinds)
    {
        var dejavu = EnsureComp<SlimeDejavuComponent>(target);
        dejavu.Origin = At(target);
        dejavu.RewindAt = _timing.CurTime + TimeSpan.FromSeconds(10);
        dejavu.Rewinds = rewinds;
        dejavu.Damage.Clear();
        if (TryComp<DamageableComponent>(target, out var damageable))
        {
            foreach (var (type, amount) in _damageable.GetAllDamage((target, damageable)).DamageDict)
            {
                dejavu.Damage[type] = amount;
            }
        }
    }

    private void UpdateDejavu(TimeSpan now)
    {
        var query = EntityQueryEnumerator<SlimeDejavuComponent>();
        while (query.MoveNext(out var uid, out var dejavu))
        {
            if (now < dejavu.RewindAt)
                continue;

            if (dejavu.Origin.IsValid(EntityManager))
            {
                _transform.SetCoordinates(uid, dejavu.Origin);
                _transform.AttachToGridOrMap(uid);
            }

            if (TryComp<DamageableComponent>(uid, out var damageable))
            {
                var spec = new DamageSpecifier();
                var current = _damageable.GetAllDamage((uid, damageable));
                foreach (var (type, amount) in current.DamageDict)
                {
                    var old = dejavu.Damage.GetValueOrDefault(type);
                    if (amount != old)
                        spec.DamageDict[type] = old - amount;
                }
                foreach (var (type, old) in dejavu.Damage)
                {
                    if (!current.DamageDict.ContainsKey(type))
                        spec.DamageDict[type] = old;
                }
                _damageable.TryChangeDamage(uid, spec, ignoreResistances: true, interruptsDoAfters: false);
            }

            PopupUser("slime-cross-dejavu", uid);
            dejavu.Rewinds--;
            if (dejavu.Rewinds <= 0)
                RemCompDeferred<SlimeDejavuComponent>(uid);
            else
                dejavu.RewindAt = now + TimeSpan.FromSeconds(10);
        }
    }

    #endregion

    #region Ледяной блок и клон

    /// <summary>status_effect/frozenstasis: существо заключено в неуязвимый ледяной блок.</summary>
    public void FreezeInIce(EntityUid target, bool resistable)
    {
        var block = Spawn(resistable ? "SlimeIceStasis" : "SlimeIceStasisIrresistable", At(target));
        var stasis = EnsureComp<SlimeIceStasisComponent>(block);
        stasis.Frozen = target;
        if (TryComp<Content.Shared.Storage.Components.EntityStorageComponent>(block, out var storage))
            _entityStorage.Insert(target, block, storage);
        if (!HasComp<GodmodeComponent>(target))
            _godmode.EnableGodmode(target);
    }

    /// <summary>status_effect/slime_clone: управляемая копия из слизи.</summary>
    public void MakeSlimeClone(EntityUid owner)
    {
        var proto = MetaData(owner).EntityPrototype?.ID;
        if (proto == null)
            return;

        var clone = Spawn(proto, At(owner));
        _meta.SetEntityName(clone, Name(owner));
        var cloneComp = EnsureComp<SlimeCloneComponent>(clone);
        cloneComp.Original = owner;
        cloneComp.NextDecay = _timing.CurTime + TimeSpan.FromSeconds(1);
        SetTint(clone, "clone", CloneTint);

        if (_mind.TryGetMind(owner, out var mindId, out var mind))
        {
            cloneComp.Mind = mindId;
            _mind.TransferTo(mindId, clone, mind: mind);
        }
    }

    /// <summary>slime_clone_decay: 1 урона каждого типа в секунду.</summary>
    private void UpdateClones(TimeSpan now)
    {
        var query = EntityQueryEnumerator<SlimeCloneComponent>();
        while (query.MoveNext(out var uid, out var clone))
        {
            if (now < clone.NextDecay)
                continue;
            clone.NextDecay = now + TimeSpan.FromSeconds(1);
            Damage(uid, "Poison", 1f);
            Damage(uid, "Asphyxiation", 1f);
            Damage(uid, "Blunt", 1f);
            Damage(uid, "Heat", 1f);
        }
    }

    /// <summary>Клон пал: разум возвращается в тело, клон растекается.</summary>
    private void OnCloneState(Entity<SlimeCloneComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
            return;
        EndClone(ent);
    }

    private void EndClone(Entity<SlimeCloneComponent> ent)
    {
        if (ent.Comp.Mind is { } mindId && !TerminatingOrDeleted(ent.Comp.Original)
            && TryComp<Content.Shared.Mind.MindComponent>(mindId, out var mind) && mind.OwnedEntity == ent.Owner)
        {
            _mind.TransferTo(mindId, ent.Comp.Original, mind: mind);
        }

        Popup("slime-cross-clone-dissolve", ent, ("target", ent.Owner));
        QueueDel(ent);
    }

    #endregion
}

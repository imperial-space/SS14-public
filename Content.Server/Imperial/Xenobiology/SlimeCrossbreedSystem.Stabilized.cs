using Robust.Shared.Map.Components;
using Content.Shared.Random.Helpers;
using Content.Shared.Prototypes;
using Content.Shared.Coordinates.Helpers;
using Robust.Shared.Random;
using System.Linq;
using Content.Server.Administration;
using Content.Server.Imperial.Xenobiology.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Stacks;
using Content.Shared.Temperature.Components;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology;

public sealed partial class SlimeCrossbreedSystem
{
    [Dependency] private readonly QuickDialogSystem _dialog = default!;


    private void InitializeStabilized()
    {
        SubscribeLocalEvent<SlimeStabilizedComponent, MapInitEvent>(OnStabilizedInit);
        SubscribeLocalEvent<SlimeStabilizedComponent, EntityTerminatingEvent>(OnStabilizedTerminating);
        SubscribeLocalEvent<SlimeStabilizedComponent, GetVerbsEvent<AlternativeVerb>>(OnStabilizedVerbs);
        SubscribeLocalEvent<SlimeStabilizedComponent, InteractUsingEvent>(OnStabilizedInteractUsing);
    }

    private void OnStabilizedInit(Entity<SlimeStabilizedComponent> ent, ref MapInitEvent args)
    {
        if (Colour(ent) == "SlimeGold")
        {
            ent.Comp.FamiliarProto = RandomFamiliar();
            ent.Comp.FamiliarName = Loc.GetString("slime-cross-familiar-default-name");
        }
    }

    private void OnStabilizedTerminating(Entity<SlimeStabilizedComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.Holder is { } holder)
            EndStabilized(ent, holder);
    }

    private EntProtoId RandomFamiliar()
    {
        return _proto.Index(SlimeExtractSystem.FriendlyMobs).Pick(_random);
    }

    /// <summary>get_held_mob(): в руке, кармане, или на один уровень вложенности (рюкзак).</summary>
    private EntityUid? HeldMob(EntityUid uid)
    {
        var parent = Transform(uid).ParentUid;
        for (var depth = 0; depth < 3 && parent.IsValid(); depth++)
        {
            if (HasComp<MobStateComponent>(parent))
                return _container.IsEntityInContainer(uid) ? parent : null;
            parent = Transform(parent).ParentUid;
        }

        return null;
    }

    private void UpdateStabilized(TimeSpan now)
    {
        var query = EntityQueryEnumerator<SlimeStabilizedComponent>();
        while (query.MoveNext(out var uid, out var stab))
        {
            if (now < stab.NextTick)
                continue;
            stab.NextTick = now + TimeSpan.FromSeconds(1);

            var holder = HeldMob(uid);
            if (stab.Holder != holder)
            {
                if (stab.Holder is { } old)
                {
                    EndStabilized((uid, stab), old);
                    PopupUser("slime-cross-stabilized-faded", old, ("colour", ColourName(uid)));
                }

                stab.Holder = holder;
                if (holder is { } fresh)
                    StartStabilized((uid, stab), fresh);
            }

            if (stab.Holder is { } current && !TerminatingOrDeleted(current))
                TickStabilized((uid, stab), current);
        }
    }

    private string ColourName(EntityUid uid)
    {
        return TryComp<SlimeCrossbreedComponent>(uid, out var cross) && _proto.TryIndex(cross.SlimeType, out var type)
            ? Loc.GetString(type.Name)
            : string.Empty;
    }

    /// <summary>on_apply() стабилизированных эффектов.</summary>
    private void StartStabilized(Entity<SlimeStabilizedComponent> ent, EntityUid holder)
    {
        switch (Colour(ent))
        {
            case "SlimeBlue":
                AddEffect(holder, "stab-blue", null);
                break;
            case "SlimeCerulean":
            {
                var proto = MetaData(holder).EntityPrototype?.ID;
                if (proto != null && HasComp<HumanoidProfileComponent>(holder))
                {
                    var clone = Spawn(proto, At(holder));
                    _meta.SetEntityName(clone, Name(holder));
                    ent.Comp.Clone = clone;
                }
                break;
            }
            case "SlimeGreen":
                PopupUser("slime-cross-stabilized-green-on", holder);
                ent.Comp.OriginalName = Name(holder);
                _meta.SetEntityName(holder, RandomHumanName(holder));
                break;
            case "SlimePink":
                PopupUser("slime-cross-stabilized-pink-on", holder, ("extract", ent.Owner));
                ent.Comp.PinkPeaceful = true;
                break;
            case "SlimeLightPink":
                AddEffect(holder, "stab-lightpink", null);
                break;
            case "SlimeAdamantine":
                AddEffect(holder, "stab-adamantine", null);
                break;
        }
    }

    /// <summary>on_remove() стабилизированных эффектов.</summary>
    private void EndStabilized(Entity<SlimeStabilizedComponent> ent, EntityUid holder)
    {
        switch (Colour(ent))
        {
            case "SlimeBlue":
                RemoveEffect(holder, "stab-blue");
                break;
            case "SlimeSepia":
                _speed.SetModifier(holder, "stab-sepia", 1f);
                break;
            case "SlimeRed":
                _speed.SetModifier(holder, "stab-red", 1f);
                break;
            case "SlimeCerulean":
                if (ent.Comp.Clone is { } clone && !TerminatingOrDeleted(clone))
                {
                    Popup("slime-cross-clone-dissolve", clone, ("target", clone));
                    QueueDel(clone);
                }
                ent.Comp.Clone = null;
                break;
            case "SlimePyrite":
                ClearTint(holder, "stab-pyrite");
                break;
            case "SlimeGreen":
                PopupUser("slime-cross-stabilized-green-off", holder);
                if (ent.Comp.OriginalName != null)
                    _meta.SetEntityName(holder, ent.Comp.OriginalName);
                ent.Comp.OriginalName = null;
                break;
            case "SlimePink":
                foreach (var mob in ent.Comp.PinkTracked.Keys)
                {
                    if (!TerminatingOrDeleted(mob))
                        Unignore(mob, holder);
                }
                ent.Comp.PinkTracked.Clear();
                break;
            case "SlimeGold":
                if (ent.Comp.Familiar is { } familiar && !TerminatingOrDeleted(familiar))
                    QueueDel(familiar);
                ent.Comp.Familiar = null;
                break;
            case "SlimeLightPink":
                RemoveEffect(holder, "stab-lightpink");
                break;
            case "SlimeAdamantine":
                RemoveEffect(holder, "stab-adamantine");
                break;
        }
    }

    /// <summary>tick() стабилизированных эффектов (раз в секунду).</summary>
    private void TickStabilized(Entity<SlimeStabilizedComponent> ent, EntityUid holder)
    {
        var coords = At(holder);
        switch (Colour(ent))
        {
            case "SlimeGrey":
                foreach (var slime in _lookup.GetEntitiesInRange<SlimeComponent>(coords, 1.5f))
                {
                    if (TryComp<SlimeBrainComponent>(slime, out var brain) && !brain.Friends.Contains(holder))
                    {
                        PopupUser("slime-cross-stabilized-grey", holder, ("extract", ent.Owner), ("slime", slime.Owner));
                        _slime.Befriend(slime, holder);
                    }
                }
                break;
            case "SlimeOrange":
                if (TryComp<TemperatureComponent>(holder, out var temp))
                {
                    var target = 310.15f;
                    var offset = Math.Clamp(target - temp.CurrentTemperature, -5f, 5f);
                    if (!MathHelper.CloseTo(offset, 0f))
                        _temperature.ForceChangeTemperature(holder, temp.CurrentTemperature + offset, temp);
                }
                break;
            case "SlimePurple":
                Heal(holder, 0.2f, 0.2f, 0.2f);
                break;
            case "SlimeSilver":
                // hunger_mod *= 0.8: возвращаем пятую часть потраченной сытости.
                if (TryComp<HungerComponent>(holder, out var silverHunger))
                    _hunger.ModifyHunger(holder, silverHunger.ActualDecayRate * 0.2f, silverHunger);
                break;
            case "SlimeMetal":
                if (++ent.Comp.Cooldown >= 30)
                {
                    ent.Comp.Cooldown = 0;
                    var stacks = new List<(EntityUid, StackComponent)>();
                    CollectStacks(holder, stacks, 0);
                    if (stacks.Count > 0)
                    {
                        var (stackUid, stack) = _random.Pick(stacks);
                        _stack.SetCount((stackUid, stack), stack.Count + 1);
                        PopupUser("slime-cross-stabilized-metal", holder, ("extract", ent.Owner), ("stack", (object) stackUid));
                    }
                }
                break;
            case "SlimeYellow":
                if (++ent.Comp.Cooldown >= 10)
                {
                    ent.Comp.Cooldown = 0;
                    ChargeOneBattery(holder, 0.1f);
                }
                break;
            case "SlimeDarkPurple":
                // Горящие пальцы: еда в активной руке готовится.
                if (_hands.GetActiveItem(holder) is { } held && HasComp<EdibleComponent>(held)
                    && TryComp<TemperatureComponent>(held, out var heldTemp) && heldTemp.CurrentTemperature < 373f)
                {
                    _temperature.ForceChangeTemperature(held, heldTemp.CurrentTemperature + 20f, heldTemp);
                }
                break;
            case "SlimeDarkBlue":
                if (TryComp<FlammableComponent>(holder, out var flammable) && flammable.FireStacks > 0 && _random.Prob(0.8f))
                {
                    _flammable.AdjustFireStacks(holder, -1f, flammable);
                    if (flammable.FireStacks <= 0)
                        PopupUser("slime-cross-stabilized-darkblue", holder, ("extract", ent.Owner));
                }

                if (_hands.GetActiveItem(holder) is { } wet && _tag.HasTag(wet, MonkeyCubeTag))
                {
                    PopupUser("slime-cross-stabilized-darkblue-cube", holder, ("extract", ent.Owner), ("item", wet));
                    if (_solutions.TryGetSolution(wet, "cube", out var cubeSol, out _))
                        _solutions.TryAddReagent(cubeSol.Value, "Water", 5);
                    else
                    {
                        Spawn("MobMonkey", At(wet));
                        QueueDel(wet);
                    }
                }
                break;
            case "SlimeBluespace":
                TickStabilizedBluespace(ent, holder);
                break;
            case "SlimeSepia":
                if (_random.Prob(0.5f) && ent.Comp.SepiaMod > -1)
                {
                    ent.Comp.SepiaMod--;
                    _speed.SetModifier(holder, "stab-sepia", 1.33f);
                }
                else if (ent.Comp.SepiaMod < 1)
                {
                    ent.Comp.SepiaMod++;
                    _speed.SetModifier(holder, "stab-sepia", 1f);
                }
                break;
            case "SlimeCerulean":
                if (_mobState.IsDead(holder))
                {
                    if (ent.Comp.Clone is { } clone && !TerminatingOrDeleted(clone) && !_mobState.IsDead(clone))
                    {
                        Popup("slime-cross-stabilized-cerulean", holder, ("target", holder), ("extract", ent.Owner));
                        if (_mind.TryGetMind(holder, out var mindId, out var mind))
                            _mind.TransferTo(mindId, clone, mind: mind);
                        ent.Comp.Clone = null;
                    }
                    else
                    {
                        PopupUser("slime-cross-stabilized-cerulean-fail", holder, ("extract", ent.Owner));
                    }

                    QueueDel(ent);
                }
                break;
            case "SlimePyrite":
                SetTint(holder, "stab-pyrite", Color.FromHsv(new System.Numerics.Vector4(_random.NextFloat(), 1f, 1f, 1f)));
                break;
            case "SlimeRed":
                // Обнуляет замедление от снаряжения: компенсируем модификатор одежды.
                _speed.SetModifier(holder, "stab-red", EquipmentSlowdownCompensation(holder));
                break;
            case "SlimePink":
                TickStabilizedPink(ent, holder);
                break;
            case "SlimeGold":
                if (ent.Comp.Familiar is not { } fam || TerminatingOrDeleted(fam) || _mobState.IsDead(fam))
                {
                    if (ent.Comp.FamiliarProto is { } famProto)
                    {
                        var familiar = Spawn(famProto, coords);
                        _meta.SetEntityName(familiar, ent.Comp.FamiliarName);
                        MakeAlly(familiar, holder);
                        ent.Comp.Familiar = familiar;
                    }
                }
                break;
            case "SlimeOil":
                if (_mobState.IsDead(holder))
                {
                    // explosion(1, 2, 4, flame 5)
                    _explosion.QueueExplosion(_transform.ToMapCoordinates(coords), "Default", 140f, 10f, 30f, holder);
                    QueueDel(ent);
                }
                break;
            case "SlimeBlack":
                TickStabilizedBlack(holder);
                break;
            case "SlimeLightPink":
                foreach (var mob in _lookup.GetEntitiesInRange<HumanoidProfileComponent>(coords, 1.5f))
                {
                    if (mob.Owner == holder || _mobState.IsDead(mob) || !_mobState.IsCritical(mob))
                        continue;
                    if (HasReagent(mob, "Epinephrine"))
                        continue;
                    PopupUser("slime-cross-stabilized-lightpink", holder, ("extract", ent.Owner), ("target", mob.Owner));
                    InjectReagent(mob, "Epinephrine", 5f);
                }
                break;
            case "SlimeRainbow":
                if (_mobState.IsCritical(holder) && ent.Comp.RegenCore is { } core && !TerminatingOrDeleted(core)
                    && TryComp<SlimeRegenerativeComponent>(core, out var regen))
                {
                    _container.TryRemoveFromContainer(core);
                    UseRegenerative((core, regen), holder, holder);
                    ent.Comp.RegenCore = null;
                    Popup("slime-cross-stabilized-rainbow", holder, ("target", holder));
                    QueueDel(ent);
                }
                break;
        }
    }

    private bool HasReagent(EntityUid uid, string reagent)
    {
        return TryComp<Content.Shared.Body.Components.BloodstreamComponent>(uid, out var stream)
               && _solutions.TryGetSolution(uid, stream.BloodSolutionName, out _, out var solution)
               && solution.GetTotalPrototypeQuantity(reagent) > 0;
    }

    private void CollectStacks(EntityUid uid, List<(EntityUid, StackComponent)> list, int depth)
    {
        if (depth > 4 || !TryComp<ContainerManagerComponent>(uid, out var manager))
            return;
        foreach (var container in manager.Containers.Values)
        {
            foreach (var child in container.ContainedEntities)
            {
                if (TryComp<StackComponent>(child, out var stack) && TryComp<Content.Shared.Materials.MaterialComponent>(child, out _)
                    && stack.Count < _stack.GetMaxCount(stack))
                    list.Add((child, stack));
                CollectStacks(child, list, depth + 1);
            }
        }
    }

    /// <summary>stabilized/red: movespeed_mod immunity к equipment_speedmod.</summary>
    private float EquipmentSlowdownCompensation(EntityUid holder)
    {
        var current = 1f;
        var slots = _inventory.GetSlotEnumerator(holder);
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is { } worn && TryComp<Content.Shared.Clothing.ClothingSpeedModifierComponent>(worn, out var mod))
                current *= mod.SprintModifier;
        }

        return current < 1f && current > 0.05f ? 1f / current : 1f;
    }

    /// <summary>stabilized/bluespace: при резкой потере здоровья — телепорт в безопасное место.</summary>
    private void TickStabilizedBluespace(Entity<SlimeStabilizedComponent> ent, EntityUid holder)
    {
        if (!TryComp<Content.Shared.Damage.Components.DamageableComponent>(holder, out var damageable))
            return;
        var damage = _damageable.GetTotalDamage((holder, damageable)).Float();

        if (_timing.CurTime >= ent.Comp.BluespaceCooldown && ent.Comp.LastHealth is { } last && damage - last > 5f)
        {
            Popup("slime-cross-stabilized-bluespace", holder, ("extract", ent.Owner), ("target", holder));
            _audio.PlayPvs(Sparks, holder);
            _items.BlinkMob(holder, 15);
            PopupUser("slime-cross-stabilized-bluespace-cooldown", holder, ("extract", ent.Owner));
            ent.Comp.BluespaceCooldown = _timing.CurTime + TimeSpan.FromMinutes(2);
        }

        ent.Comp.LastHealth = damage;
    }

    /// <summary>stabilized/pink: пока никто не ранен, существа вокруг не нападают.</summary>
    private void TickStabilizedPink(Entity<SlimeStabilizedComponent> ent, EntityUid holder)
    {
        var now = _timing.CurTime;
        var coords = At(holder);

        foreach (var (mob, lastDamage) in ent.Comp.PinkTracked.ToList())
        {
            if (TerminatingOrDeleted(mob))
            {
                ent.Comp.PinkTracked.Remove(mob);
                continue;
            }

            var damage = TryComp<Content.Shared.Damage.Components.DamageableComponent>(mob, out var dmg) ? _damageable.GetTotalDamage((mob, dmg)).Float() : 0f;
            if (damage > lastDamage + 0.01f)
            {
                ent.Comp.BrokenPeaceUntil = now + TimeSpan.FromMinutes(2);
                ent.Comp.PinkTracked[mob] = damage;
            }

            if ((_transform.GetMapCoordinates(mob).Position - _transform.ToMapCoordinates(coords).Position).Length() > 7f)
            {
                Unignore(mob, holder);
                ent.Comp.PinkTracked.Remove(mob);
            }
        }

        foreach (var mob in _lookup.GetEntitiesInRange<Content.Shared.NPC.Components.NpcFactionMemberComponent>(coords, 7f))
        {
            if (mob.Owner == holder || HasComp<HumanoidProfileComponent>(mob) || ent.Comp.PinkTracked.ContainsKey(mob))
                continue;
            var damage = TryComp<Content.Shared.Damage.Components.DamageableComponent>(mob, out var dmg) ? _damageable.GetTotalDamage((mob, dmg)).Float() : 0f;
            ent.Comp.PinkTracked[mob] = damage;
            if (now >= ent.Comp.BrokenPeaceUntil)
                PinkIgnore(mob.Owner, holder);
        }

        var broken = now < ent.Comp.BrokenPeaceUntil;
        if (broken && ent.Comp.PinkPeaceful)
        {
            ent.Comp.PinkPeaceful = false;
            foreach (var mob in ent.Comp.PinkTracked.Keys)
            {
                Unignore(mob, holder);
            }

            PopupUser("slime-cross-stabilized-pink-broken", holder);
        }
        else if (!broken && !ent.Comp.PinkPeaceful)
        {
            ent.Comp.PinkPeaceful = true;
            foreach (var mob in ent.Comp.PinkTracked.Keys)
            {
                PinkIgnore(mob, holder);
            }

            PopupUser("slime-cross-stabilized-pink-on", holder, ("extract", ent.Owner));
        }
    }

    /// <summary>remove_ally: исключение снимается вместе с компонентом, если его создал экстракт.</summary>
    private void Unignore(EntityUid mob, EntityUid holder)
    {
        if (_pinkCreated.Remove(mob))
            RemComp<Content.Shared.NPC.Components.FactionExceptionComponent>(mob);
    }

    private void PinkIgnore(EntityUid mob, EntityUid holder)
    {
        if (!HasComp<Content.Shared.NPC.Components.FactionExceptionComponent>(mob))
            _pinkCreated.Add(mob);
        _faction.IgnoreEntity(mob, holder);
    }

    private readonly HashSet<EntityUid> _pinkCreated = new();

    /// <summary>stabilized/black: удушающий захват высасывает жизнь.</summary>
    private void TickStabilizedBlack(EntityUid holder)
    {
        if (!TryComp<PullerComponent>(holder, out var puller) || puller.Pulling is not { } drained
            || !HasComp<MobStateComponent>(drained) || _mobState.IsDead(drained))
            return;

        // В SS14 нет удушающего захвата: достаточно тащить жертву, лёжа ей неспособную сопротивляться.
        if (!_mobState.IsIncapacitated(drained) && !HasComp<Content.Shared.Stunnable.KnockedDownComponent>(drained))
            return;

        Heal(holder, 1f, 0f, 0f);
        if (TryComp<HungerComponent>(holder, out var hunger))
            _hunger.ModifyHunger(holder, 3f, hunger);
        Damage(drained, "Blunt", 1.2f, holder);
    }

    private string RandomHumanName(EntityUid holder)
    {
        var first = new[] { "Алекс", "Сэм", "Джордан", "Тейлор", "Морган", "Кейси", "Райли", "Джейми", "Робин", "Саша" };
        var last = new[] { "Смит", "Ли", "Джонсон", "Браун", "Кларк", "Уокер", "Холл", "Янг", "Кинг", "Райт" };
        return $"{_random.Pick(first)} {_random.Pick(last)}";
    }

    #region Золото и радуга

    /// <summary>stabilized/gold/attack_self(): настройка фамильяра.</summary>
    private void OnStabilizedVerbs(Entity<SlimeStabilizedComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || Colour(ent) != "SlimeGold")
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("slime-cross-familiar-location"),
            Act = () =>
            {
                ResetFamiliar(ent);
                PopupUser("slime-cross-familiar-location-done", user, ("extract", ent.Owner));
            },
        });
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("slime-cross-familiar-species"),
            Act = () =>
            {
                ent.Comp.FamiliarProto = RandomFamiliar();
                ResetFamiliar(ent);
                PopupUser("slime-cross-familiar-species-done", user, ("extract", ent.Owner));
            },
        });
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("slime-cross-familiar-name"),
            Act = () =>
            {
                if (!TryComp<ActorComponent>(user, out var actor))
                    return;
                _dialog.OpenDialog<string>(actor.PlayerSession, Loc.GetString("slime-cross-familiar-name"), Loc.GetString("slime-cross-familiar-name-prompt"),
                    name =>
                    {
                        name = name.Trim();
                        if (name.Length > 32)
                            name = name[..32];
                        if (!string.IsNullOrWhiteSpace(name))
                            ent.Comp.FamiliarName = name;
                        ResetFamiliar(ent);
                        PopupUser("slime-cross-familiar-name-done", user, ("extract", ent.Owner));
                    });
            },
        });
    }

    private void ResetFamiliar(Entity<SlimeStabilizedComponent> ent)
    {
        if (ent.Comp.Familiar is { } familiar && !TerminatingOrDeleted(familiar))
            QueueDel(familiar);
        ent.Comp.Familiar = null;
    }

    /// <summary>stabilized/rainbow/item_interaction(): принимает регенеративный экстракт.</summary>
    private void OnStabilizedInteractUsing(Entity<SlimeStabilizedComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || Colour(ent) != "SlimeRainbow" || !HasComp<SlimeRegenerativeComponent>(args.Used))
            return;
        args.Handled = true;

        if (ent.Comp.RegenCore != null)
        {
            PopupUser("slime-cross-stabilized-rainbow-full", args.User, ("extract", ent.Owner));
            return;
        }

        var container = _container.EnsureContainer<ContainerSlot>(ent, "regen_core");
        if (!_container.Insert(args.Used, container))
            return;
        ent.Comp.RegenCore = args.Used;
        PopupUser("slime-cross-stabilized-rainbow-insert", args.User, ("extract", ent.Owner), ("core", args.Used));
    }

    #endregion
}

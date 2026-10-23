using System.Linq;
using Content.Server.Polymorph.Components;
using Content.Shared.Actions.Components;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Polymorph;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Wizard.Spells;

/// <summary>summonitem: отмеченный предмет возвращается в руку откуда угодно.</summary>
[RegisterComponent]
public sealed partial class ImperialSummonItemSpellComponent : Component
{
    [ViewVariables]
    public EntityUid? Marked;

    [ViewVariables]
    public string BaseName = string.Empty;
}

/// <summary>charge: перезарядить предмет в руке или вернуть откаты тащимому магу.</summary>
[RegisterComponent]
public sealed partial class ImperialChargeSpellComponent : Component;

/// <summary>tap: обновить все заклинания ценой 20 единиц максимального здоровья.</summary>
[RegisterComponent]
public sealed partial class ImperialSoulTapSpellComponent : Component;

/// <summary>shapeshift/wizard: облик выбирается один раз, повторный каст возвращает обратно.</summary>
[RegisterComponent]
public sealed partial class ImperialShapeshiftSpellComponent : Component
{
    /// <summary>possible_shapes: существо → прототип превращения.</summary>
    [DataField(required: true)]
    public List<EntProtoId> Shapes = new();

    [ViewVariables]
    public EntProtoId? Chosen;
}

/// <summary>Сколько здоровья отнял soul tap (у лича и духа души нет).</summary>
[RegisterComponent]
public sealed partial class ImperialSoulTappedComponent : Component
{
    [ViewVariables]
    public float Taken;
}

public sealed partial class ImperialWizardSpellsSystem
{
    [Dependency] private readonly ImperialListInputSystem _listInput = default!;

    private const float TapHealth = 20f;

    private void InitializeAssistance()
    {
        SubscribeLocalEvent<ImperialSummonItemSpellComponent, ImperialSpellEffectEvent>(OnSummonItem);
        SubscribeLocalEvent<ImperialSummonItemSpellComponent, ImperialSpellChannelDoAfterEvent>(OnUnmarkDone);
        SubscribeLocalEvent<ImperialChargeSpellComponent, ImperialSpellEffectEvent>(OnCharge);
        SubscribeLocalEvent<BatteryComponent, ImperialMagicChargeEvent>(OnBatteryCharged);
        SubscribeLocalEvent<ImperialSoulTapSpellComponent, ImperialSpellEffectEvent>(OnSoulTap);
        SubscribeLocalEvent<ImperialShapeshiftSpellComponent, ImperialSpellEffectEvent>(OnShapeshift);
    }

    #region Призыв предмета

    /// <summary>cast(): отметить предмет в руке, снять отметку или призвать отмеченный.</summary>
    private void OnSummonItem(Entity<ImperialSummonItemSpellComponent> ent, ref ImperialSpellEffectEvent args)
    {
        var caster = args.Caster;
        if (string.IsNullOrEmpty(ent.Comp.BaseName))
            ent.Comp.BaseName = Name(ent);

        if (ent.Comp.Marked is not { } marked || TerminatingOrDeleted(marked))
        {
            ent.Comp.Marked = null;
            if (!_hands.TryGetActiveItem(caster, out var held))
            {
                _popup.PopupEntity(Loc.GetString("imperial-spell-summon-nothing"), caster, caster);
                return;
            }

            args.Handled = true;
            ent.Comp.Marked = held;
            _meta.SetEntityName(ent, Loc.GetString("imperial-spell-summon-recall", ("item", held.Value)));
            _popup.PopupEntity(Loc.GetString("imperial-spell-summon-marked", ("item", held.Value)), caster, caster);
            return;
        }

        if (_hands.TryGetActiveItem(caster, out var active) && active == marked)
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("imperial-spell-summon-unmarking", ("item", marked)), caster, caster);
            _doAfter.TryStartDoAfter(new Content.Shared.DoAfter.DoAfterArgs(EntityManager, caster, TimeSpan.FromSeconds(5),
                new ImperialSpellChannelDoAfterEvent(), ent, marked)
            {
                BreakOnMove = true,
            });
            return;
        }

        args.Handled = true;
        Recall(caster, marked);
    }

    private void OnUnmarkDone(Entity<ImperialSummonItemSpellComponent> ent, ref ImperialSpellChannelDoAfterEvent args)
    {
        if (args.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-summon-keep"), args.User, args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString("imperial-spell-summon-unmarked"), args.User, args.User);
        ent.Comp.Marked = null;
        _meta.SetEntityName(ent, ent.Comp.BaseName);
    }

    /// <summary>try_recall_item(): вытащить из контейнеров (до держащего существа) и вложить в руку.</summary>
    private void Recall(EntityUid caster, EntityUid item)
    {
        var toRetrieve = item;
        for (var depth = 0; depth < 10 && _containers.TryGetContainingContainer(toRetrieve, out var container); depth++)
        {
            var holder = container.Owner;
            if (HasComp<MobStateComponent>(holder))
                break;

            if (Transform(holder).Anchored)
                break;

            toRetrieve = holder;
        }

        _popup.PopupEntity(Loc.GetString("imperial-spell-summon-vanish", ("item", toRetrieve)), toRetrieve, PopupType.Small);
        if (_containers.TryGetContainingContainer(toRetrieve, out var final))
            _containers.Remove(toRetrieve, final, force: true);

        _transform.SetCoordinates(toRetrieve, Transform(caster).Coordinates);
        if (!_hands.TryPickupAnyHand(caster, toRetrieve))
            _transform.AttachToGridOrMap(toRetrieve);

        _audio.PlayPvs(new Robust.Shared.Audio.SoundPathSpecifier("/Audio/Imperial/Wizard/summonitems_generic.ogg"), caster);
    }

    #endregion

    #region Заряд

    /// <summary>charge: тащимый маг получает свежие заклинания, иначе — предмет в руке.</summary>
    private void OnCharge(Entity<ImperialChargeSpellComponent> ent, ref ImperialSpellEffectEvent args)
    {
        args.Handled = true;
        var caster = args.Caster;

        if (TryComp<PullerComponent>(caster, out var puller) && puller.Pulling is { } pulled && HasComp<MobStateComponent>(pulled))
        {
            if (ResetSpells(pulled))
            {
                _popup.PopupEntity(Loc.GetString("imperial-spell-charge-flow"), pulled, pulled);
                _popup.PopupEntity(Loc.GetString("imperial-spell-charge-warm", ("target", pulled)), caster, caster);
                return;
            }

            _popup.PopupEntity(Loc.GetString("imperial-spell-charge-strange"), pulled, pulled);
        }

        if (!_hands.TryGetActiveItem(caster, out var item) && !_hands.EnumerateHeld(caster).Any())
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-charge-fades"), caster, caster);
            return;
        }

        var toCharge = item ?? _hands.EnumerateHeld(caster).First();
        var ev = new ImperialMagicChargeEvent(caster);
        RaiseLocalEvent(toCharge, ref ev);
        foreach (var contained in AllContained(toCharge))
        {
            RaiseLocalEvent(contained, ref ev);
        }

        if (ev.BurntOut)
            _popup.PopupEntity(Loc.GetString("imperial-spell-charge-burnt", ("item", toCharge)), caster, caster, PopupType.MediumCaution);
        else if (ev.Charged)
            _popup.PopupEntity(Loc.GetString("imperial-spell-charge-done", ("item", toCharge)), caster, caster);
        else
            _popup.PopupEntity(Loc.GetString("imperial-spell-charge-nothing", ("item", toCharge)), caster, caster);
    }

    private IEnumerable<EntityUid> AllContained(EntityUid uid)
    {
        if (!TryComp<ContainerManagerComponent>(uid, out var manager))
            yield break;

        foreach (var container in _containers.GetAllContainers(uid, manager))
        {
            foreach (var contained in container.ContainedEntities)
            {
                yield return contained;
            }
        }
    }

    /// <summary>Батареи и энергоячейки заряжаются полностью.</summary>
    private void OnBatteryCharged(Entity<BatteryComponent> ent, ref ImperialMagicChargeEvent args)
    {
        args.Charged = true;
        EntityManager.System<Content.Shared.Power.EntitySystems.SharedBatterySystem>().SetCharge(ent.AsNullable(), ent.Comp.MaxCharge);
    }

    /// <summary>reset_spell_cooldown() для всех заклинаний существа.</summary>
    private bool ResetSpells(EntityUid uid)
    {
        var any = false;
        foreach (var action in _actions.GetActions(uid))
        {
            if (!HasComp<ImperialSpellComponent>(action.Owner))
                continue;

            _actions.ClearCooldown(action.Owner);
            any = true;
        }

        return any;
    }

    #endregion

    #region Высасывание души

    private void OnSoulTap(Entity<ImperialSoulTapSpellComponent> ent, ref ImperialSpellEffectEvent args)
    {
        if (HasComp<ImperialLichComponent>(args.Caster) || HasComp<ImperialSpiritComponent>(args.Caster))
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-tap-no-soul"), args.Caster, args.Caster);
            return;
        }

        args.Handled = true;
        SoulTap(args.Caster);
    }

    /// <summary>tap.cast(): −20 к максимуму здоровья, все заклинания готовы; без здоровья — смерть.</summary>
    public void SoulTap(EntityUid target)
    {
        var tapped = EnsureComp<ImperialSoulTappedComponent>(target);
        tapped.Taken += TapHealth;
        foreach (var state in new[] { MobState.Critical, MobState.Dead })
        {
            if (_thresholds.TryGetThresholdForState(target, state, out var threshold))
                _thresholds.SetMobStateThreshold(target, Content.Shared.FixedPoint.FixedPoint2.Max(1, threshold.Value - TapHealth), state);
        }

        ResetSpells(target);
        var remaining = _thresholds.TryGetThresholdForState(target, MobState.Dead, out var dead) ? dead.Value.Float() : 0f;
        if (remaining <= TapHealth)
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-tap-consumed"), target, target, PopupType.LargeCaution);
            EnsureComp<ImperialLichComponent>(target);
            _mobs.ChangeMobState(target, MobState.Dead);
            return;
        }

        _popup.PopupEntity(Loc.GetString(remaining - TapHealth <= TapHealth ? "imperial-spell-tap-drained-hard" : "imperial-spell-tap-drained"),
            target, target, PopupType.MediumCaution);
    }

    #endregion

    #region Смена облика

    /// <summary>shapeshift: облик выбирается раз и навсегда; в облике каст возвращает обратно.</summary>
    private void OnShapeshift(Entity<ImperialShapeshiftSpellComponent> ent, ref ImperialSpellEffectEvent args)
    {
        var caster = args.Caster;
        if (TryComp<PolymorphedEntityComponent>(caster, out var polymorphed))
        {
            args.Handled = true;
            _polymorph.Revert((caster, polymorphed));
            return;
        }

        if (ent.Comp.Chosen is { } chosen)
        {
            args.Handled = Shapeshift(caster, chosen);
            return;
        }

        if (!TryComp<ActorComponent>(caster, out var actor))
            return;

        // show_radial_menu(): выбор формы. Откат — после превращения.
        var names = ent.Comp.Shapes.ToDictionary(s => _proto.Index(s).Name, s => s);
        var spellUid = ent.Owner;
        _listInput.Open(actor.PlayerSession, Loc.GetString("imperial-spell-shapeshift-title"), Loc.GetString("imperial-spell-shapeshift-prompt"),
            names.Keys.ToList(), choice =>
            {
                if (!names.TryGetValue(choice, out var shape) || TerminatingOrDeleted(spellUid) || !TryComp<ImperialShapeshiftSpellComponent>(spellUid, out var spell)
                    || !TryComp<ImperialSpellComponent>(spellUid, out var spellComp))
                    return;

                spell.Chosen = shape;
                if (Shapeshift(caster, shape))
                    _actions.SetCooldown(spellUid, _spells.GetCooldown(spellComp));
            });
    }

    private bool Shapeshift(EntityUid caster, EntProtoId shape)
    {
        var config = new PolymorphConfiguration
        {
            Entity = shape,
            Forced = true,
            Inventory = PolymorphInventoryChange.None,
            TransferName = true,
            TransferDamage = true,
            RevertOnCrit = true,
            RevertOnDeath = true,
            AllowRepeatedMorphs = false,
        };
        return _polymorph.PolymorphEntity(caster, config) != null;
    }

    #endregion
}

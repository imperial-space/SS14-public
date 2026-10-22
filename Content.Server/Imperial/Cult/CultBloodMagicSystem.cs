using System.Linq;
using Content.Server.Imperial.Cult.Components;
using Content.Server.Popups;
using Content.Server.Stack;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage.Components;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Emp;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Cult;
using Content.Shared.Imperial.Cult.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Stacks;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using NewStatus = Content.Shared.StatusEffectNew.StatusEffectsSystem;

namespace Content.Server.Imperial.Cult;

/// <summary>Магия крови (blood_magic.dm).</summary>
public sealed class CultBloodMagicSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly CultSystem _cult = default!;
    [Dependency] private readonly CultRuneSystem _runes = default!;
    [Dependency] private readonly CultStructureSystem _structures = default!;
    [Dependency] private readonly CultConstructSystem _constructs = default!;
    [Dependency] private readonly CultItemSystem _items = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedEmpSystem _emp = default!;
    [Dependency] private readonly SharedCuffableSystem _cuffs = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedBloodstreamSystem _blood = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly NewStatus _status = default!;

    public const int RunelessMax = 1;
    public const int MaxCharge = 4;
    public const int EnhancedMax = 5;
    private const float UsesToBlood = 2f;
    private const int BloodDrainGain = 50;
    private const float SelfHealPenalty = 1.65f;
    private const float Ss13BloodNormal = 560f;
    private const float BloodSafeFraction = 0.85f;
    public const int HalberdCost = 150;
    public const int BarrageCost = 300;
    public const int BeamCost = 500;
    private const int IronToShell = 50;

    private static readonly EntProtoId HallucinationEffect = "StatusEffectSeeingRainbow";

    /// <summary>Порядок subtypesof(/datum/action/innate/cult/blood_spell).</summary>
    public static readonly (CultSpell Spell, EntProtoId Action)[] SpellActions =
    {
        (CultSpell.Stun, "ActionCultSpellStun"),
        (CultSpell.Teleport, "ActionCultSpellTeleport"),
        (CultSpell.Emp, "ActionCultSpellEmp"),
        (CultSpell.Shackles, "ActionCultSpellShackles"),
        (CultSpell.Construction, "ActionCultSpellConstruction"),
        (CultSpell.Equipment, "ActionCultSpellEquipment"),
        (CultSpell.Dagger, "ActionCultSpellDagger"),
        (CultSpell.Horror, "ActionCultSpellHorror"),
        (CultSpell.Veiling, "ActionCultSpellVeiling"),
        (CultSpell.BloodRites, "ActionCultSpellBloodRites"),
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CultistComponent, CultPrepareBloodMagicActionEvent>(OnPrepareAction);
        SubscribeLocalEvent<CultistComponent, CultBloodMagicActionEvent>(OnLegacyPrepare);
        SubscribeLocalEvent<CultistComponent, CultCarveSpellDoAfterEvent>(OnCarveDoAfter);
        SubscribeLocalEvent<CultistComponent, CultBloodSpellActionEvent>(OnSpellAction);
        SubscribeLocalEvent<CultistComponent, CultHallucinationsActionEvent>(OnHorror);
        SubscribeLocalEvent<CultistComponent, CultShackleDoAfterEvent>(OnShackleDoAfter);
        SubscribeLocalEvent<CultistComponent, CultConstructionDoAfterEvent>(OnConstructionDoAfter);

        SubscribeLocalEvent<CultBloodSpellComponent, ComponentShutdown>(OnSpellShutdown);

        SubscribeLocalEvent<CultAuraComponent, AfterInteractEvent>(OnAuraInteract);
        SubscribeLocalEvent<CultAuraComponent, MeleeHitEvent>(OnAuraMelee);
        SubscribeLocalEvent<CultAuraComponent, UseInHandEvent>(OnAuraUseInHand);
        SubscribeLocalEvent<CultAuraComponent, ComponentShutdown>(OnAuraShutdown);
    }

    #region Подготовка

    private void OnLegacyPrepare(Entity<CultistComponent> ent, ref CultBloodMagicActionEvent args)
    {
        args.Handled = true;
        OpenPrepare(ent, false);
    }

    private void OnPrepareAction(Entity<CultistComponent> ent, ref CultPrepareBloodMagicActionEvent args)
    {
        args.Handled = true;
        OpenPrepare(ent, false);
    }

    private bool NearEmpowerRune(EntityUid user)
    {
        foreach (var rune in _lookup.GetEntitiesInRange<CultRuneComponent>(Transform(user).Coordinates, 1.5f))
        {
            if (rune.Comp.RuneType == CultRuneType.Empower)
                return true;
        }
        return false;
    }

    /// <summary>datum/action/innate/cult/blood_magic/Activate().</summary>
    public void OpenPrepare(EntityUid user, bool onRune)
    {
        if (!TryComp<CultistComponent>(user, out var cultist))
            return;

        var rune = onRune || NearEmpowerRune(user);
        var limit = rune ? cultist.MagicEnhanced ? EnhancedMax : MaxCharge : RunelessMax;

        if (cultist.Spells.Count >= limit)
        {
            _cult.Message(user, Loc.GetString(rune ? "cult-magic-limit-rune" : "cult-magic-limit-runeless", ("limit", limit), ("runeless", RunelessMax)));
            OpenRemove(user, cultist, () => OpenSpellList(user, rune, limit));
            return;
        }

        OpenSpellList(user, rune, limit);
    }

    private void OpenRemove(EntityUid user, CultistComponent cultist, Action? after)
    {
        var options = cultist.Spells.Where(Exists).Select(s => new CultMenuOption(GetNetEntity(s).ToString(), Name(s))).ToList();
        if (options.Count == 0)
        {
            after?.Invoke();
            return;
        }

        _cult.OpenChoice(user, Loc.GetString("cult-magic-remove-title"), options, id =>
        {
            if (!NetEntity.TryParse(id, out var net) || !TryGetEntity(net, out var spell))
                return;
            RemoveSpell(user, spell.Value);
            after?.Invoke();
        }, radial: false);
    }

    private void OpenSpellList(EntityUid user, bool rune, int limit)
    {
        var options = new List<CultMenuOption>();
        foreach (var (spell, action) in SpellActions)
        {
            if (!_proto.TryIndex(action, out var proto))
                continue;
            options.Add(new CultMenuOption(spell.ToString(), proto.Name));
        }
        options.Add(new CultMenuOption("remove", Loc.GetString("cult-magic-remove-option")));

        _cult.OpenChoice(user, Loc.GetString("cult-magic-prepare-title"), options, id =>
        {
            if (!TryComp<CultistComponent>(user, out var cultist))
                return;

            if (id == "remove")
            {
                OpenRemove(user, cultist, null);
                return;
            }

            if (!Enum.TryParse<CultSpell>(id, out var spell))
                return;
            if (_mobState.IsIncapacitated(user) || rune && !NearEmpowerRune(user) || cultist.Spells.Count >= limit)
                return;

            _cult.Message(user, Loc.GetString("cult-magic-carve-start"));
            _cult.SoundTo(user, CultSounds.Slice, CultSystem.Db(10));

            if (cultist.ChannelingMagic)
            {
                _cult.Message(user, Loc.GetString("cult-magic-already-channeling"));
                return;
            }
            cultist.ChannelingMagic = true;

            var time = rune ? 4f : 10f;
            if (cultist.MagicEnhanced)
                time *= 0.5f;

            var doAfter = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(time), new CultCarveSpellDoAfterEvent { Spell = spell, OnRune = rune }, user)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
            };
            if (!_doAfter.TryStartDoAfter(doAfter))
                cultist.ChannelingMagic = false;
        }, radial: false);
    }

    private void OnCarveDoAfter(Entity<CultistComponent> ent, ref CultCarveSpellDoAfterEvent args)
    {
        ent.Comp.ChannelingMagic = false;
        if (args.Cancelled || args.Handled)
            return;
        args.Handled = true;

        if (HasComp<HumanoidProfileComponent>(ent))
            ModifyBloodSs13(ent, -(args.OnRune ? 8 : 40));

        var spell = GrantSpell(ent, args.Spell);
        if (spell != null)
            _cult.Message(ent, Loc.GetString("cult-magic-prepared", ("spell", Name(spell.Value))));
    }

    public EntityUid? GrantSpell(Entity<CultistComponent> ent, CultSpell spell)
    {
        var proto = SpellActions.FirstOrDefault(s => s.Spell == spell).Action;
        EntityUid? action = null;
        if (!_actions.AddAction(ent, ref action, proto) || action == null)
            return null;

        var comp = EnsureComp<CultBloodSpellComponent>(action.Value);
        comp.Owner = ent;
        comp.BaseDescription = MetaData(action.Value).EntityDescription;
        ent.Comp.Spells.Add(action.Value);
        UpdateSpellDescription(action.Value, comp);
        return action;
    }

    private void UpdateSpellDescription(EntityUid action, CultBloodSpellComponent comp)
    {
        var desc = comp.BaseDescription;
        if (comp.HealthCost > 0)
            desc += "\n" + Loc.GetString("cult-magic-health-cost", ("cost", comp.HealthCost));
        desc += "\n" + Loc.GetString("cult-magic-uses", ("uses", comp.Charges));
        _meta.SetEntityDescription(action, desc);
    }

    public void RemoveSpell(EntityUid user, EntityUid spell)
    {
        if (TryComp<CultistComponent>(user, out var cultist))
            cultist.Spells.Remove(spell);
        _actions.RemoveAction(user, spell);
        if (!TerminatingOrDeleted(spell))
            QueueDel(spell);
    }

    public void ClearAllSpells(Entity<CultistComponent> ent)
    {
        foreach (var spell in ent.Comp.Spells.ToList())
            RemoveSpell(ent, spell);
        ent.Comp.Spells.Clear();
    }

    private void OnSpellShutdown(Entity<CultBloodSpellComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Hand is { } hand && !TerminatingOrDeleted(hand))
        {
            if (TryComp<CultAuraComponent>(hand, out var aura))
                aura.Source = null;
            QueueDel(hand);
        }
        if (ent.Comp.Owner is { } owner && TryComp<CultistComponent>(owner, out var cultist))
            cultist.Spells.Remove(ent);
    }

    private void ConsumeCharge(EntityUid user, EntityUid action, CultBloodSpellComponent spell)
    {
        spell.Charges--;
        UpdateSpellDescription(action, spell);
        if (spell.Charges <= 0 && spell.DeletesOnEmpty)
            RemoveSpell(user, action);
    }

    #endregion

    #region Заклинания-действия

    private void OnSpellAction(Entity<CultistComponent> ent, ref CultBloodSpellActionEvent args)
    {
        var action = args.Action.Owner;
        if (!TryComp<CultBloodSpellComponent>(action, out var spell))
            return;
        args.Handled = true;
        if (_mobState.IsIncapacitated(ent) || spell.Charges <= 0 && spell.DeletesOnEmpty)
            return;

        var user = ent.Owner;
        switch (spell.Spell)
        {
            case CultSpell.Emp:
                _cult.Whisper(user, spell.Invocation ?? "Ta'gh fara'qha fel d'amar det!");
                _popup.PopupEntity(Loc.GetString("cult-spell-emp-others", ("user", user)), user, Filter.PvsExcept(user), true, PopupType.MediumCaution);
                _cult.Message(user, Loc.GetString("cult-spell-emp-self"));
                _emp.EmpPulse(_transform.GetMapCoordinates(user), 5f, 50000f, TimeSpan.FromSeconds(30), user);
                ConsumeCharge(user, action, spell);
                return;

            case CultSpell.Dagger:
                _cult.Whisper(user, spell.Invocation ?? "Wur d'dai leev'mai k'sagan!");
                _popup.PopupEntity(Loc.GetString("cult-spell-dagger-others", ("user", user)), user, Filter.PvsExcept(user), true, PopupType.Medium);
                _cult.Message(user, Loc.GetString("cult-spell-dagger-self"));
                var blade = Spawn("CultDagger", Transform(user).Coordinates);
                if (_hands.TryPickupAnyHand(user, blade))
                    _cult.Message(user, Loc.GetString("cult-spell-dagger-hand", ("item", blade)));
                else
                    _popup.PopupEntity(Loc.GetString("cult-spell-dagger-feet", ("item", blade), ("user", user)), user, PopupType.Medium);
                _cult.SoundTo(user, CultSounds.Magic, CultSystem.Db(25));
                ConsumeCharge(user, action, spell);
                return;

            case CultSpell.Veiling:
                CastVeiling(user, action, spell);
                return;
        }

        if (spell.Aura == null)
            return;

        // Активация убирает/даёт ауру в руку.
        if (spell.Hand is { } hand && !TerminatingOrDeleted(hand))
        {
            QueueDel(hand);
            spell.Hand = null;
            _cult.Message(user, Loc.GetString("cult-spell-snuff"));
            return;
        }

        var aura = Spawn(spell.Aura.Value, Transform(user).Coordinates);
        var auraComp = EnsureComp<CultAuraComponent>(aura);
        auraComp.Source = action;
        auraComp.Uses = spell.Charges;
        auraComp.HealthCost = spell.HealthCost;
        if (!_hands.TryPickupAnyHand(user, aura))
        {
            auraComp.Source = null;
            Del(aura);
            _cult.Message(user, Loc.GetString("cult-spell-no-hand"));
            return;
        }
        spell.Hand = aura;
        _cult.Message(user, Loc.GetString("cult-spell-invoke", ("spell", Name(action))));
    }

    private void CastVeiling(EntityUid user, EntityUid action, CultBloodSpellComponent spell)
    {
        _cult.Whisper(user, spell.Invocation ?? "Kla'atu barada nikt'o!");
        if (!spell.Revealing)
        {
            _popup.PopupEntity(Loc.GetString("cult-spell-veil-others", ("user", user)), user, Filter.PvsExcept(user), true, PopupType.Medium);
            _cult.Message(user, Loc.GetString("cult-spell-veil-self"));
            _cult.SoundTo(user, CultSounds.Smoke, CultSystem.Db(25));
            _structures.ConcealAround(_transform.GetMapCoordinates(user), 5f);
            spell.Revealing = true;
            _meta.SetEntityName(action, Loc.GetString("cult-spell-reveal-name"));
            _actions.SetIcon(action, new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/BloodCult/actions.rsi"), "back"));
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("cult-spell-reveal-others", ("user", user)), user, Filter.PvsExcept(user), true, PopupType.Medium);
            _cult.Message(user, Loc.GetString("cult-spell-reveal-self"));
            _cult.SoundTo(user, CultSounds.EnterBlood, CultSystem.Db(25));
            _structures.RevealAround(_transform.GetMapCoordinates(user), 7f, structureRange: 6f);
            spell.Revealing = false;
            _meta.SetEntityName(action, Loc.GetString("cult-spell-conceal-name"));
            _actions.SetIcon(action, new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/BloodCult/actions.rsi"), "gone"));
        }
        ConsumeCharge(user, action, spell);
    }

    /// <summary>Hallucinations: человек не-культист в радиусе 7.</summary>
    private void OnHorror(Entity<CultistComponent> ent, ref CultHallucinationsActionEvent args)
    {
        var action = args.Action.Owner;
        if (!TryComp<CultBloodSpellComponent>(action, out var spell))
            return;
        var target = args.Target;
        if (!HasComp<HumanoidProfileComponent>(target) || _cult.IsCultist(target))
            return;
        var a = _transform.GetMapCoordinates(ent);
        var b = _transform.GetMapCoordinates(target);
        if (a.MapId != b.MapId || (a.Position - b.Position).Length() > 7.5f)
            return;

        args.Handled = true;
        _status.TryUpdateStatusEffectDuration(target, HallucinationEffect, TimeSpan.FromSeconds(240));
        _cult.SoundTo(ent, CultSounds.Ghost, CultSystem.Db(50));
        var mark = EnsureComp<CultHorrorMarkComponent>(target);
        mark.End = _timing.CurTime + TimeSpan.FromMinutes(4);
        Dirty(target, mark);
        _cult.Message(ent, Loc.GetString("cult-spell-horror-cursed", ("target", target)));

        spell.Charges--;
        UpdateSpellDescription(action, spell);
        if (spell.Charges <= 0)
        {
            _cult.Message(ent, Loc.GetString("cult-spell-horror-exhausted"));
            RemoveSpell(ent, action);
        }
    }

    #endregion

    #region Ауры

    private void OnAuraShutdown(Entity<CultAuraComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Source is not { } source || TerminatingOrDeleted(source) || !TryComp<CultBloodSpellComponent>(source, out var spell))
            return;

        spell.Hand = null;
        if (ent.Comp.Uses <= 0 && spell.DeletesOnEmpty)
        {
            if (spell.Owner is { } owner)
                RemoveSpell(owner, source);
            return;
        }
        spell.Charges = ent.Comp.Uses;
        UpdateSpellDescription(source, spell);
    }

    private void OnAuraUseInHand(Entity<CultAuraComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        if (ent.Comp.Spell == CultSpell.BloodRites)
        {
            OpenRites(ent, args.User);
            return;
        }
        Cast(ent, args.User, args.User);
    }

    private void OnAuraInteract(Entity<CultAuraComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        if (!_cult.IsCultist(args.User))
        {
            ent.Comp.Uses = 0;
            QueueDel(ent);
            args.Handled = true;
            return;
        }

        if (args.Target is { } target)
        {
            args.Handled = true;
            Cast(ent, target, args.User);
            return;
        }

        if (ent.Comp.Spell == CultSpell.BloodRites)
        {
            args.Handled = true;
            BloodDraw(ent, args.ClickLocation, args.User);
            BaseCast(ent, args.User);
        }
    }

    private void OnAuraMelee(Entity<CultAuraComponent> ent, ref MeleeHitEvent args)
    {
        if (args.HitEntities.Count == 0)
            return;
        args.Handled = true;
        Cast(ent, args.HitEntities[0], args.User);
    }

    /// <summary>cast_spell() базовый: шёпот, урон в руку, удаление при 0 зарядов.</summary>
    private void BaseCast(Entity<CultAuraComponent> ent, EntityUid user)
    {
        if (ent.Comp.Invocation != null)
            _cult.Whisper(user, ent.Comp.Invocation);
        if (ent.Comp.HealthCost > 0)
            _cult.Damage(user, "Slash", ent.Comp.HealthCost);
        if (ent.Comp.Uses <= 0)
        {
            QueueDel(ent);
            return;
        }
        if (ent.Comp.Source is { } source && TryComp<CultBloodSpellComponent>(source, out var spell))
        {
            spell.Charges = ent.Comp.Uses;
            UpdateSpellDescription(source, spell);
        }
    }

    private void Cast(Entity<CultAuraComponent> ent, EntityUid target, EntityUid user)
    {
        switch (ent.Comp.Spell)
        {
            case CultSpell.Stun:
                if (CastStun(ent, target, user))
                    BaseCast(ent, user);
                break;
            case CultSpell.Teleport:
                CastTeleport(ent, target, user);
                break;
            case CultSpell.Shackles:
                CastShackles(ent, target, user);
                break;
            case CultSpell.Construction:
                CastConstruction(ent, target, user);
                break;
            case CultSpell.Equipment:
                if (CastEquipment(ent, target, user))
                    BaseCast(ent, user);
                break;
            case CultSpell.BloodRites:
                if (CastRites(ent, target, user))
                    BaseCast(ent, user);
                break;
        }
    }

    private bool CastStun(Entity<CultAuraComponent> ent, EntityUid target, EntityUid user)
    {
        if (!HasComp<Content.Shared.Mobs.Components.MobStateComponent>(target) || _cult.IsCultist(target))
            return false;

        var coef = 1f;
        if (_cult.GetRule() is { } rule)
        {
            if (rule.Comp.Ascendent)
                coef = 0.1f;
            else if (rule.Comp.Risen)
                coef = 0.4f;
        }

        _popup.PopupEntity(Loc.GetString("cult-spell-stun-others", ("user", user)), user, Filter.PvsExcept(user), true, PopupType.MediumCaution);
        _cult.Message(user, Loc.GetString("cult-spell-stun-self", ("target", target)));
        _cult.FlashLight(user, Color.FromHex("#A01010"), 1.1f, 2f, 0.2f);
        ent.Comp.Uses--;

        if (CultForeignComponents.Has(EntityManager, target, CultForeignComponents.Heretic))
        {
            _stun.TryKnockdown(target, TimeSpan.FromSeconds(0.5), true);
            _cult.FlashLight(target, Color.FromHex("#00FF00"), 1.5f, 2.5f, 0.5f);
            _audio.PlayPvs(CultSounds.MagicBlockMind, target);
            _cult.Message(user, Loc.GetString("cult-spell-stun-heretic-user", ("target", target)));
            _cult.Message(target, Loc.GetString("cult-spell-stun-heretic-target", ("user", user)));
            _popup.PopupEntity(Loc.GetString("cult-spell-stun-absorbed"), target, PopupType.Medium);
            return true;
        }

        if (_cult.BlocksMagic(target))
        {
            _cult.Message(user, Loc.GetString("cult-spell-no-effect"));
            return true;
        }

        _cult.Message(user, Loc.GetString("cult-spell-stun-success", ("target", target)));
        _cult.Paralyze(target, 16f * coef);
        _cult.Flash(target, user);
        if (HasComp<BorgChassisComponent>(target))
        {
            _emp.EmpPulse(_transform.GetMapCoordinates(target), 0.5f, 100000f, TimeSpan.FromSeconds(20), user);
        }
        else if (HasComp<HumanoidProfileComponent>(target))
        {
            _cult.Silence(target, 12f * coef);
            _cult.Stutter(target, 30f * coef);
            _cult.CultSlur(target, 30f * coef);
            _cult.Jitter(target, 30f * coef);
        }
        return true;
    }

    private void CastTeleport(Entity<CultAuraComponent> ent, EntityUid target, EntityUid user)
    {
        if (!_cult.IsCultist(target))
        {
            _cult.Message(user, Loc.GetString("cult-spell-teleport-cultists-only"));
            return;
        }

        var options = new List<CultMenuOption>();
        var keys = new HashSet<string>();
        var query = EntityQueryEnumerator<CultRuneComponent>();
        while (query.MoveNext(out var uid, out var rune))
        {
            if (rune.RuneType != CultRuneType.Teleport)
                continue;
            var key = rune.ListKey ?? Name(uid);
            var unique = key;
            var n = 1;
            while (!keys.Add(unique))
                unique = $"{key} ({++n})";
            options.Add(new CultMenuOption(GetNetEntity(uid).ToString(), unique));
        }

        if (options.Count == 0)
        {
            _cult.Message(user, Loc.GetString("cult-rune-teleport-none"));
            return;
        }

        _cult.OpenChoice(user, Loc.GetString("cult-rune-teleport-title"), options, id =>
        {
            if (TerminatingOrDeleted(ent) || !_hands.IsHolding(user, ent) || _mobState.IsIncapacitated(user))
                return;
            if (!NetEntity.TryParse(id, out var net) || !TryGetEntity(net, out var dest) || !HasComp<CultRuneComponent>(dest))
            {
                _cult.Message(user, Loc.GetString("cult-spell-teleport-invalid"));
                return;
            }

            var destCoords = Transform(dest.Value).Coordinates;
            if (_structures.IsBlocked(destCoords))
            {
                _cult.Message(user, Loc.GetString("cult-spell-teleport-blocked"));
                return;
            }

            ent.Comp.Uses--;
            var origin = Transform(user).Coordinates;
            if (!_runes.CultTeleport(target, destCoords))
            {
                BaseCast(ent, user);
                return;
            }
            _popup.PopupCoordinates(Loc.GetString("cult-spell-teleport-crack", ("user", user)), origin, PopupType.MediumCaution);
            _cult.Message(user, Loc.GetString("cult-spell-teleport-self"));
            _popup.PopupCoordinates(Loc.GetString("cult-rune-teleport-boom"), destCoords, PopupType.MediumCaution);
            _audio.PlayPvs(CultSounds.PortalEnter, origin, AudioParams.Default.WithVolume(CultSystem.Db(50)).WithVariation(0.05f));
            _audio.PlayPvs(CultSounds.PortalEnter, destCoords, AudioParams.Default.WithVolume(CultSystem.Db(50)).WithVariation(0.05f));
            BaseCast(ent, user);
        }, radial: false);
    }

    private void CastShackles(Entity<CultAuraComponent> ent, EntityUid target, EntityUid user)
    {
        if (!HasComp<HumanoidProfileComponent>(target) || _cult.IsCultist(target))
            return;
        if (!TryComp<CuffableComponent>(target, out var cuffable))
            return;
        if (!TryComp<Content.Shared.Hands.Components.HandsComponent>(target, out var hands) || hands.Count < 2)
        {
            _popup.PopupEntity(Loc.GetString("cult-spell-shackles-arms"), user, PopupType.Medium);
            return;
        }

        if (_cuffs.IsCuffed((target, cuffable)))
        {
            _cult.Message(user, Loc.GetString("cult-spell-shackles-bound", ("target", target)));
            BaseCast(ent, user);
            return;
        }

        _audio.PlayPvs(CultSounds.CableCuff, user, AudioParams.Default.WithVolume(CultSystem.Db(30)).WithVariation(0.05f));
        _popup.PopupEntity(Loc.GetString("cult-spell-shackles-start-others", ("user", user), ("target", target)), target, Filter.PvsExcept(target), true, PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString("cult-spell-shackles-start-target", ("user", user)), target, target, PopupType.LargeCaution);

        var doAfter = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(3), new CultShackleDoAfterEvent(), user, target: target, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnShackleDoAfter(Entity<CultistComponent> ent, ref CultShackleDoAfterEvent args)
    {
        if (args.Handled || args.Used is not { } used || !TryComp<CultAuraComponent>(used, out var aura))
            return;
        args.Handled = true;
        var target = args.Target;

        if (args.Cancelled || target == null)
        {
            if (target != null)
                _cult.Message(ent, Loc.GetString("cult-spell-shackles-fail", ("target", target.Value)));
            BaseCast((used, aura), ent);
            return;
        }

        if (TryComp<CuffableComponent>(target, out var cuffable) && !_cuffs.IsCuffed((target.Value, cuffable)))
        {
            var cuffs = Spawn("CultShackles", Transform(target.Value).Coordinates);
            if (_cuffs.TryAddNewCuffs(target.Value, ent, cuffs, cuffable))
            {
                _cult.Silence(target.Value, 10);
                _cult.Message(ent, Loc.GetString("cult-spell-shackles-done", ("target", target.Value)));
                aura.Uses--;
            }
            else
            {
                Del(cuffs);
            }
        }
        else
        {
            _cult.Message(ent, Loc.GetString("cult-spell-shackles-bound", ("target", target.Value)));
        }
        BaseCast((used, aura), ent);
    }

    private void CastConstruction(Entity<CultAuraComponent> ent, EntityUid target, EntityUid user)
    {
        if (ent.Comp.Channeling)
        {
            _cult.Message(user, Loc.GetString("cult-spell-construction-channeling"));
            return;
        }

        var coords = Transform(target).Coordinates;
        var protoId = MetaData(target).EntityPrototype?.ID;

        if (TryComp<StackComponent>(target, out var stack))
        {
            if (stack.StackTypeId == "Steel")
            {
                // В SS14 стопка стали — максимум 30 листов (в SS13 — 50): собираем сталь со всего тайла и из рук.
                var steel = SteelStacks(target, user);
                if (steel.Sum(s => s.Comp.Count) < IronToShell)
                {
                    _cult.Message(user, Loc.GetString("cult-spell-construction-iron", ("count", IronToShell)));
                    return;
                }
                var need = IronToShell;
                foreach (var s in steel)
                {
                    var take = Math.Min(need, s.Comp.Count);
                    _stack.ReduceCount((s.Owner, s.Comp), take);
                    need -= take;
                    if (need <= 0)
                        break;
                }
                ent.Comp.Uses--;
                _cult.Message(user, Loc.GetString("cult-spell-construction-shell"));
                Spawn("CultConstructShell", coords);
                _cult.SoundTo(user, CultSounds.Magic, CultSystem.Db(25));
                BaseCast(ent, user);
                return;
            }

            if (stack.StackTypeId == "Plasteel")
            {
                var quantity = stack.Count;
                ent.Comp.Uses--;
                Del(target);
                _stack.SpawnAtPosition(quantity, "CultRunedMetal", coords);
                _cult.Message(user, Loc.GetString("cult-spell-construction-runed"));
                _cult.SoundTo(user, CultSounds.Magic, CultSystem.Db(25));
                BaseCast(ent, user);
                return;
            }
        }

        if (HasComp<BorgChassisComponent>(target))
        {
            if (_constructs.BorgHasMind(target))
            {
                ent.Comp.Channeling = true;
                _popup.PopupEntity(Loc.GetString("cult-spell-construction-cloud", ("user", user), ("target", target)), target, PopupType.LargeCaution);
                _audio.PlayPvs(CultSounds.AlienPrying, coords, AudioParams.Default.WithVolume(CultSystem.Db(80)).WithVariation(0.05f));
                var tint = EnsureComp<CultTintComponent>(target);
                tint.Color = Color.Black;
                Dirty(target, tint);

                var doAfter = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(9), new CultConstructionDoAfterEvent(), user, target: target, used: ent)
                {
                    BreakOnMove = true,
                    NeedHand = true,
                };
                if (!_doAfter.TryStartDoAfter(doAfter))
                {
                    ent.Comp.Channeling = false;
                    RemComp<CultTintComponent>(target);
                }
                return;
            }

            ent.Comp.Uses--;
            _cult.Message(user, Loc.GetString("cult-spell-construction-borg-shell", ("target", target)));
            Spawn("CultConstructShell", coords);
            _cult.SoundTo(user, CultSounds.Magic, CultSystem.Db(25));
            QueueDel(target);
            BaseCast(ent, user);
            return;
        }

        if (HasComp<AirlockComponent>(target) && !HasComp<CultRunedDoorComponent>(target))
        {
            ent.Comp.Channeling = true;
            _audio.PlayPvs(CultSounds.AirlockForced, coords, AudioParams.Default.WithVolume(CultSystem.Db(50)).WithVariation(0.05f));
            _cult.Sparks(coords);
            var doAfter = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(5), new CultConstructionDoAfterEvent(), user, target: target, used: ent)
            {
                BreakOnMove = true,
                NeedHand = true,
            };
            if (!_doAfter.TryStartDoAfter(doAfter))
                ent.Comp.Channeling = false;
            return;
        }

        if (TryComp<CultSoulstoneComponent>(target, out var stone))
        {
            if (!_constructs.CorruptStone((target, stone)))
                return;
            ent.Comp.Uses--;
            _cult.Message(user, Loc.GetString("cult-spell-construction-corrupt", ("target", target)));
            _cult.SoundTo(user, CultSounds.Magic, CultSystem.Db(25));
            BaseCast(ent, user);
            return;
        }

        _ = protoId;
        _cult.Message(user, Loc.GetString("cult-spell-construction-invalid", ("target", target)));
    }

    private void OnConstructionDoAfter(Entity<CultistComponent> ent, ref CultConstructionDoAfterEvent args)
    {
        if (args.Handled || args.Used is not { } used || !TryComp<CultAuraComponent>(used, out var aura))
            return;
        args.Handled = true;
        aura.Channeling = false;
        if (args.Target is not { } target || TerminatingOrDeleted(target))
            return;

        if (args.Cancelled)
        {
            RemComp<CultTintComponent>(target);
            return;
        }

        var user = ent.Owner;
        var coords = Transform(target).Coordinates;

        if (HasComp<BorgChassisComponent>(target))
        {
            _emp.EmpPulse(_transform.GetMapCoordinates(target), 0.5f, 100000f, TimeSpan.FromSeconds(20), user);
            aura.Channeling = true;
            _constructs.OpenConstructChoice(user, type =>
            {
                aura.Channeling = false;
                if (TerminatingOrDeleted(target) || TerminatingOrDeleted(used) || _mobState.IsIncapacitated(user))
                {
                    if (!TerminatingOrDeleted(target))
                        RemComp<CultTintComponent>(target);
                    return;
                }
                _popup.PopupEntity(Loc.GetString("cult-spell-construction-borg-done", ("target", target), ("type", Loc.GetString($"cult-construct-name-{type.ToString().ToLowerInvariant()}"))), target, PopupType.LargeCaution);
                _constructs.MakeConstruct(type, target, user, coords, fromMind: target, stoneCaptured: false);
                aura.Uses--;
                QueueDel(target);
                BaseCast((used, aura), user);
            });
            return;
        }

        if (HasComp<AirlockComponent>(target))
        {
            _structures.NarsieActAirlock(target);
            aura.Uses--;
            _popup.PopupEntity(Loc.GetString("cult-spell-construction-airlock", ("user", user)), user, PopupType.MediumCaution);
            _cult.SoundTo(user, CultSounds.Magic, CultSystem.Db(25));
            BaseCast((used, aura), user);
        }
    }

    /// <summary>Стопки стали: цель первой, затем на том же тайле и в руках.</summary>
    private List<Entity<StackComponent>> SteelStacks(EntityUid target, EntityUid user)
    {
        var result = new List<Entity<StackComponent>>();
        var seen = new HashSet<EntityUid>();
        void Add(EntityUid uid)
        {
            if (seen.Add(uid) && TryComp<StackComponent>(uid, out var s) && s.StackTypeId == "Steel" && !TerminatingOrDeleted(uid))
                result.Add((uid, s));
        }

        Add(target);
        foreach (var near in _lookup.GetEntitiesInRange<StackComponent>(Transform(target).Coordinates, 0.45f))
            Add(near.Owner);
        foreach (var held in _hands.EnumerateHeld(user))
            Add(held);
        return result;
    }

    private bool CastEquipment(Entity<CultAuraComponent> ent, EntityUid target, EntityUid user)
    {
        if (!HasComp<HumanoidProfileComponent>(target) || !_cult.IsCultist(target))
            return false;

        ent.Comp.Uses--;
        _popup.PopupEntity(Loc.GetString("cult-spell-equipment", ("target", target)), target, PopupType.Medium);

        EquipOrDelete(target, "ClothingUniformJumpsuitColorBlack", "jumpsuit");
        EquipOrDelete(target, "ClothingOuterCultRobesAlt", "outerClothing");
        EquipOrDelete(target, "ClothingShoesCultAlt", "shoes");
        EquipOrDelete(target, "ClothingBackpackCult", "back");

        if (target == user)
            QueueDel(ent);

        var coords = Transform(target).Coordinates;
        var dagger = Spawn("CultDagger", coords);
        _hands.TryPickupAnyHand(target, dagger);
        var bola = Spawn("CultBola", coords);
        _hands.TryPickupAnyHand(target, bola);
        return true;
    }

    private void EquipOrDelete(EntityUid target, string proto, string slot)
    {
        var item = Spawn(proto, Transform(target).Coordinates);
        if (!_inventory.TryEquip(target, item, slot, silent: true, force: true))
            Del(item);
    }

    #endregion

    #region Кровавые обряды

    /// <summary>Уровень крови в единицах SS13 (норма 560).</summary>
    public float BloodSs13(EntityUid uid)
    {
        return _blood.GetBloodLevel(uid) * Ss13BloodNormal;
    }

    public void ModifyBloodSs13(EntityUid uid, float ss13Units)
    {
        if (!TryComp<BloodstreamComponent>(uid, out var bloodstream))
            return;
        var reference = bloodstream.BloodReferenceSolution;
        var scale = reference.Volume.Float() / Ss13BloodNormal;
        _blood.TryModifyBloodLevel((uid, bloodstream), FixedPoint2.New(ss13Units * scale));
    }

    private bool CastRites(Entity<CultAuraComponent> ent, EntityUid target, EntityUid user)
    {
        if (HasComp<CultConstructComponent>(target) || HasComp<CultShadeComponent>(target))
            return HealConstruct(ent, target, user);

        if (HasComp<PuddleComponent>(target))
        {
            BloodDraw(ent, Transform(target).Coordinates, user);
            return true;
        }

        if (!HasComp<HumanoidProfileComponent>(target))
            return false;

        if (!HasComp<BloodstreamComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("cult-rites-no-blood"), target, user);
            return false;
        }
        if (_mobState.IsDead(target))
        {
            _popup.PopupEntity(Loc.GetString("cult-rites-dead"), target, user);
            return false;
        }

        return _cult.IsCultist(target) ? HealCultist(ent, target, user) : DrainVictim(ent, target, user);
    }

    private bool HealConstruct(Entity<CultAuraComponent> ent, EntityUid target, EntityUid user)
    {
        if (!_cult.IsCultAligned(target) || !TryComp<DamageableComponent>(target, out var damageable))
            return false;
        var missing = _cult.TotalDamage(target);
        if (missing <= 0)
        {
            _cult.Message(user, Loc.GetString("cult-rites-no-healing"));
            return false;
        }
        if (ent.Comp.Uses <= 0)
        {
            _popup.PopupEntity(Loc.GetString("cult-rites-out"), target, user);
            return false;
        }

        if (ent.Comp.Uses > missing)
        {
            _cult.HealAll(target, missing);
            _popup.PopupEntity(Loc.GetString("cult-rites-construct-full", ("target", target), ("user", user)), target, PopupType.Medium);
            ent.Comp.Uses -= (int) MathF.Ceiling(missing);
        }
        else
        {
            _cult.HealAll(target, ent.Comp.Uses);
            _popup.PopupEntity(Loc.GetString("cult-rites-construct-partial", ("target", target), ("user", user)), target, PopupType.Medium);
            ent.Comp.Uses = 0;
        }
        _audio.PlayPvs(CultSounds.StaffHealing, target, AudioParams.Default.WithVolume(CultSystem.Db(25)));
        _cult.Beam(user, Transform(target).Coordinates, "CultBeamSend", 1f);
        return true;
    }

    private bool HealCultist(Entity<CultAuraComponent> ent, EntityUid target, EntityUid user)
    {
        if (ent.Comp.Uses <= 0)
        {
            _popup.PopupEntity(Loc.GetString("cult-rites-out"), target, user);
            return false;
        }

        var donor = false;
        var safe = Ss13BloodNormal * BloodSafeFraction;
        var blood = BloodSs13(target);
        if (blood < safe)
        {
            var needed = safe - blood;
            var bank = UsesToBlood * ent.Comp.Uses;
            if (bank < needed)
            {
                ModifyBloodSs13(target, bank);
                _cult.Message(user, Loc.GetString("cult-rites-last-blood"));
                ent.Comp.Uses = 0;
                return true;
            }
            donor = true;
            ModifyBloodSs13(target, needed);
            ent.Comp.Uses -= (int) MathF.Round(needed / UsesToBlood);
            _cult.Message(user, Loc.GetString(target == user ? "cult-rites-blood-restored-self" : "cult-rites-blood-restored", ("target", target)));
        }

        var brute = _cult.GetGroupDamage(target, "Brute");
        var burn = _cult.GetGroupDamage(target, "Burn");
        var tox = _cult.GetGroupDamage(target, "Toxin");
        var oxy = _cult.GetGroupDamage(target, "Airloss");
        var overall = brute + burn + tox + oxy;
        if (overall <= 0)
        {
            if (donor)
                return true;
            _cult.Message(user, Loc.GetString("cult-rites-no-healing"));
            return false;
        }

        var healed = MathF.Min(ent.Comp.Uses, overall);
        var cost = healed;
        if (target == user)
        {
            _cult.Message(user, Loc.GetString("cult-rites-self-inefficient"));
            healed = MathF.Min(ent.Comp.Uses * (1f / SelfHealPenalty), overall);
            cost = healed * SelfHealPenalty;
        }
        ent.Comp.Uses = Math.Max(0, ent.Comp.Uses - (int) MathF.Round(cost));

        _popup.PopupEntity(Loc.GetString(ent.Comp.Uses == 0 ? "cult-rites-healed-partial" : "cult-rites-healed-full", ("target", target)), target, PopupType.Medium);
        _cult.HealGroup(target, "Airloss", healed * (oxy / overall));
        _cult.HealGroup(target, "Toxin", healed * (tox / overall));
        _cult.HealGroup(target, "Burn", healed * (burn / overall));
        _cult.HealGroup(target, "Brute", healed * (brute / overall));

        _audio.PlayPvs(CultSounds.StaffHealing, target, AudioParams.Default.WithVolume(CultSystem.Db(25)));
        _cult.Effect("CultEffectSparks", Transform(target).Coordinates, randomDir: true);
        if (user != target)
            _cult.Beam(user, Transform(target).Coordinates, "CultBeamSend", 1.5f);
        return true;
    }

    private bool DrainVictim(Entity<CultAuraComponent> ent, EntityUid target, EntityUid user)
    {
        if (_cult.HasCultSlur(target))
        {
            _cult.Message(user, Loc.GetString("cult-rites-tainted", ("target", target)));
            return false;
        }
        if (BloodSs13(target) <= Ss13BloodNormal * BloodSafeFraction)
        {
            _cult.Message(user, Loc.GetString("cult-rites-too-little", ("target", target)));
            return false;
        }

        ModifyBloodSs13(target, -BloodDrainGain * UsesToBlood);
        ent.Comp.Uses += BloodDrainGain;
        _cult.Beam(user, Transform(target).Coordinates, "CultBeamDrain", 1f);
        _audio.PlayPvs(CultSounds.EnterBlood, target, AudioParams.Default.WithVolume(CultSystem.Db(50)));
        _popup.PopupEntity(Loc.GetString("cult-rites-drain-others", ("user", user), ("target", target)), target, PopupType.MediumCaution);
        _cult.Message(user, Loc.GetString("cult-rites-drain-self", ("target", target)));
        _cult.Effect("CultEffectSparks", Transform(target).Coordinates, randomDir: true);
        return true;
    }

    /// <summary>blood_draw(): кровь с пола в радиусе 2.</summary>
    private void BloodDraw(Entity<CultAuraComponent> ent, EntityCoordinates coords, EntityUid user)
    {
        var gain = 0f;
        foreach (var puddle in _lookup.GetEntitiesInRange<PuddleComponent>(coords, 2.5f))
        {
            if (!_solutions.TryGetSolution(puddle.Owner, puddle.Comp.SolutionName, out _, out var solution))
                continue;
            var blood = solution.GetTotalPrototypeQuantity("Blood").Float();
            if (blood <= 0)
                continue;
            gain += MathF.Max(blood * 0.6f, 1f);
            _cult.Effect("CultEffectFloorGlow", Transform(puddle).Coordinates);
            QueueDel(puddle);
        }

        if (gain <= 0)
            return;

        _cult.Beam(user, coords, "CultBeamDrain", 1.5f);
        _cult.Effect("CultEffectSparks", Transform(user).Coordinates, randomDir: true);
        _audio.PlayPvs(CultSounds.EnterBlood, coords, AudioParams.Default.WithVolume(CultSystem.Db(50)));
        var charges = Math.Max(1, (int) MathF.Round(gain));
        _cult.Message(user, Loc.GetString("cult-rites-floor", ("count", charges)));
        ent.Comp.Uses += charges;
    }

    /// <summary>manipulator/attack_self(): радиальное меню продвинутых обрядов.</summary>
    private void OpenRites(Entity<CultAuraComponent> ent, EntityUid user)
    {
        var options = new List<CultMenuOption>
        {
            new("halberd", Loc.GetString("cult-rites-halberd", ("cost", HalberdCost)),
                new SpriteSpecifier.Rsi(new ResPath("/Textures/Objects/Weapons/Melee/cult_halberd.rsi"), "icon")),
            new("barrage", Loc.GetString("cult-rites-barrage", ("cost", BarrageCost)),
                new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/BloodCult/barrage.rsi"), "icon")),
            new("beam", Loc.GetString("cult-rites-beam", ("cost", BeamCost)),
                new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/BloodCult/aura.rsi"), "disintegrate")),
        };

        _cult.OpenChoice(user, Loc.GetString("cult-rites-title"), options, choice =>
        {
            if (TerminatingOrDeleted(ent) || !_hands.IsHolding(user, ent) || _mobState.IsIncapacitated(user))
            {
                _cult.Message(user, Loc.GetString("cult-rites-decide-against"));
                return;
            }

            var (cost, proto) = choice switch
            {
                "halberd" => (HalberdCost, "CultHalberd"),
                "barrage" => (BarrageCost, "CultBloodBarrage"),
                "beam" => (BeamCost, "CultBloodBeam"),
                _ => (0, string.Empty),
            };
            if (cost == 0)
                return;

            if (ent.Comp.Uses < cost)
            {
                _cult.Message(user, Loc.GetString("cult-rites-need", ("cost", cost)));
                return;
            }

            ent.Comp.Uses -= cost;
            var coords = Transform(user).Coordinates;
            QueueDel(ent);
            // Освободить руку от ауры сразу.
            _hands.TryDrop(user, ent.Owner, checkActionBlocker: false);

            var rite = Spawn(proto, coords);
            if (choice == "halberd")
            {
                _items.BindHalberd(rite, user);
                if (_hands.TryPickupAnyHand(user, rite))
                    _cult.Message(user, Loc.GetString("cult-rites-halberd-hand", ("item", rite)));
                else
                    _popup.PopupEntity(Loc.GetString("cult-rites-halberd-feet", ("item", rite), ("user", user)), user, PopupType.Medium);
                return;
            }

            if (_hands.TryPickupAnyHand(user, rite))
            {
                _cult.Message(user, Loc.GetString(choice == "beam" ? "cult-rites-beam-hand" : "cult-rites-barrage-hand"));
            }
            else
            {
                _cult.Message(user, Loc.GetString("cult-rites-need-hand"));
                Del(rite);
            }
        });
    }

    #endregion

    /// <summary>Ритуальный кинжал: святая вода → нечестивая (try_purge_holywater).</summary>
    public bool TryPurgeHolyWater(EntityUid target, EntityUid user, EntityUid item)
    {
        if (!_solutions.TryGetSolution(target, "chemicals", out var solEnt, out var solution))
            return true;
        var holy = solution.GetTotalPrototypeQuantity("Holywater");
        if (holy <= 0)
            return true;

        _cult.Message(user, Loc.GetString("cult-purge-holywater", ("target", target), ("item", item)));
        _solutions.RemoveReagent(solEnt.Value, "Holywater", holy);
        _solutions.TryAddReagent(solEnt.Value, "UnholyWater", holy, out _);
        return true;
    }
}

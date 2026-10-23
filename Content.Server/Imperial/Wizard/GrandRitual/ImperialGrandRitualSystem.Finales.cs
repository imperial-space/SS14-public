using System.Linq;
using Content.Server.Imperial.Wizard;
using Content.Server.Objectives.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Gibbing;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Content.Shared.Roles.Jobs;
using Content.Shared.Traits;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Wizard.GrandRitual;

/// <summary>/datum/grand_finale: описание варианта гранд-финала.</summary>
public sealed record ImperialGrandFinale(string Id, TimeSpan MinimumTime, TimeSpan InvokeTime, bool DireWarning);

/// <summary>Тело, ждущее воскрешения при «Увековечивании» (/obj/effect/spectre_of_resurrection).</summary>
[RegisterComponent]
public sealed partial class ImperialSpectreOfResurrectionComponent : Component
{
    [ViewVariables]
    public EntityUid? Corpse;

    [ViewVariables]
    public TimeSpan ReviveAt;
}

/// <summary>/datum/grand_finale/*: семь финалов, сырный финал и «Продолжение».</summary>
public sealed partial class ImperialGrandRitualSystem
{
    [Dependency] private readonly ImperialSpellbookSpecialsSystem _specials = default!;
    [Dependency] private readonly ImperialSpellSystem _spellSystem = default!;
    [Dependency] private readonly ImperialSpellbookSystem _spellbook = default!;
    [Dependency] private readonly ImperialListInputSystem _listInput = default!;
    [Dependency] private readonly SharedDoorSystem _doors = default!;
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly GibbingSystem _gibbing = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly TargetObjectiveSystem _targets = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;

    private const string PickNothing = "Continuation";
    private static readonly EntProtoId VendettaObjective = "ImperialWizardVendettaObjective";

    /// <summary>Порядок как в радиальном меню TG.</summary>
    private static readonly ImperialGrandFinale[] Finales =
    {
        new("Evolution", TimeSpan.Zero, TimeSpan.FromSeconds(30), false),
        new("Connection", TimeSpan.Zero, TimeSpan.FromSeconds(30), false),
        new("Annihilation", TimeSpan.FromMinutes(90), TimeSpan.FromSeconds(60), true),
        new("Transformation", TimeSpan.Zero, TimeSpan.FromSeconds(30), false),
        new("Jubilation", TimeSpan.Zero, TimeSpan.FromSeconds(30), false),
        new("Usurpation", TimeSpan.Zero, TimeSpan.FromSeconds(30), false),
        new("Perpetuation", TimeSpan.FromMinutes(30), TimeSpan.FromSeconds(30), false),
    };

    private static readonly ImperialGrandFinale CheeseFinale = new("Cheese", TimeSpan.FromMinutes(45), TimeSpan.FromSeconds(30), true);

    /// <summary>Annihilation: possible_last_words.</summary>
    private static readonly string[] LastWords =
    {
        "Пламя и разрушение!", "Ро-о-о-о-ок!!", "ХА-ХА-ХА-ХА-ХА!! А-ХА-ХА-ХА-ХА-ХА-ХА-ХА!!", "Хи-хи-хи!! Ху-ху-ху!! Ха-ха-ха-а!!",
        "О-хо-хо-хо-хо-хо!!", "Трепещите, жалкие смертные!", "Дрожите перед моим величием!", "Выбирайте бога и молитесь!",
        "Это бесполезно!", "Если бы боги хотели, чтобы вы жили, они не создали бы меня!",
        "Бог остаётся на небесах из страха перед тем, что я создал!", "Пришло разорение!", "Всё сущее, склонись перед моей волей!",
    };

    /// <summary>midas: permitted_transforms (без пиццы — её измерения в SS14 нет).</summary>
    private static readonly ImperialDimensionTheme[] MidasThemes =
    {
        new("FloorGold", "WallGold"),
        new("FloorFlesh", "WallMeat"),
        new("FloorGrass", "WallWood"),
    };

    private static readonly ProtoId<TraitPrototype> Hallucinations = "Paracusia";
    private static readonly ProtoId<Content.Shared.Roles.StartingGearPrototype> PassengerGear = "PassengerGear";
    private static readonly ProtoId<Content.Shared.Roles.JobPrototype> CaptainJob = "Captain";
    private static readonly ProtoId<Content.Shared.Roles.JobPrototype> ClownJob = "Clown";

    private bool _immortality;

    private void InitializeFinales()
    {
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    private static ImperialGrandFinale? GetFinale(string id)
    {
        return id == CheeseFinale.Id ? CheeseFinale : Finales.FirstOrDefault(f => f.Id == id);
    }

    private TimeSpan FinaleInvokeTime(string id)
    {
        return GetFinale(id)?.InvokeTime ?? BaseInvokeTime;
    }

    /// <summary>finale/interact(): выбрать эффект, затем дождаться минимального времени раунда.</summary>
    private bool FinaleInteract(Entity<ImperialGrandRuneComponent> ent, EntityUid user)
    {
        if (!ent.Comp.ChosenEffect)
        {
            SelectFinale(ent, user);
            return false;
        }

        if (ent.Comp.FinaleEffect is { } id && GetFinale(id) is { } finale && finale.MinimumTime >= _ticker.RoundDuration())
        {
            _popup.PopupEntity(Loc.GetString("imperial-grand-finale-wait", ("time", (int) Math.Ceiling((finale.MinimumTime - _ticker.RoundDuration()).TotalMinutes))), user, user);
            return false;
        }

        return true;
    }

    /// <summary>select_finale(): радиальное меню вариантов.</summary>
    private void SelectFinale(Entity<ImperialGrandRuneComponent> ent, EntityUid user)
    {
        if (!TryComp<ActorComponent>(user, out var actor))
            return;

        var options = new Dictionary<string, string>();
        foreach (var finale in Finales)
        {
            var label = Loc.GetString($"imperial-grand-finale-{finale.Id.ToLowerInvariant()}-name") + " — " +
                        Loc.GetString($"imperial-grand-finale-{finale.Id.ToLowerInvariant()}-desc");
            if (finale.MinimumTime >= _ticker.RoundDuration())
                label += " " + Loc.GetString("imperial-grand-finale-available-in", ("time", (int) Math.Ceiling((finale.MinimumTime - _ticker.RoundDuration()).TotalMinutes)));
            options[label] = finale.Id;
        }

        options[Loc.GetString("imperial-grand-finale-continuation-name") + " — " + Loc.GetString("imperial-grand-finale-continuation-desc")] = PickNothing;
        _listInput.Open(actor.PlayerSession, Loc.GetString("imperial-grand-finale-title"), Loc.GetString("imperial-grand-finale-prompt"),
            options.Keys.ToList(), choice =>
            {
                if (TerminatingOrDeleted(ent) || ent.Comp.ChosenEffect || !options.TryGetValue(choice, out var id))
                    return;

                if (GetFinale(id) is { } picked && picked.MinimumTime >= _ticker.RoundDuration())
                {
                    _popup.PopupEntity(Loc.GetString("imperial-grand-finale-wait", ("time", (int) Math.Ceiling((picked.MinimumTime - _ticker.RoundDuration()).TotalMinutes))), user, user);
                    return;
                }

                ent.Comp.ChosenEffect = true;
                if (id == PickNothing)
                    return;

                ent.Comp.FinaleEffect = id;
                ent.Comp.InvokeTime = GetInvokeTime(ent.Comp);
                _popup.PopupEntity(Loc.GetString("imperial-grand-finale-chosen"), user, user, PopupType.Medium);
            });
    }

    /// <summary>finale/invoke_rune(): громкие предупреждения для опасных финалов.</summary>
    private void FinaleDireWarning(Entity<ImperialGrandRuneComponent> ent)
    {
        if (ent.Comp.FinaleEffect is not { } id || GetFinale(id) is not { DireWarning: true })
            return;

        if (ent.Comp.DireWarningsGiven != ent.Comp.TimesInvoked)
            return;

        var area = AreaName(ent);
        string? message = ent.Comp.DireWarningsGiven switch
        {
            0 => Loc.GetString("imperial-grand-finale-dire-1", ("area", area)),
            1 => Loc.GetString("imperial-grand-finale-dire-2", ("area", area)),
            2 => Loc.GetString("imperial-grand-finale-dire-3", ("area", area)),
            _ => null,
        };

        if (message != null)
            Announce(message);

        ent.Comp.DireWarningsGiven++;
    }

    /// <summary>finale/summon_round_event().</summary>
    private void FinaleSummon(Entity<ImperialGrandRuneComponent> ent, EntityUid user)
    {
        if (ent.Comp.FinaleEffect is not { } id)
        {
            SummonRoundEvent(user);
            return;
        }

        Log.Info($"{ToPrettyString(user)} завершил гранд-финал {id}");
        switch (id)
        {
            case "Evolution":
                foreach (var human in _specials.StationHumans(null).ToList())
                {
                    _specials.GiveSummoned(human, false, 0.2f);
                }
                break;
            case "Connection":
                AllAccess(user);
                break;
            case "Annihilation":
                Armageddon(user);
                break;
            case "Transformation":
                Midas(user);
                break;
            case "Jubilation":
                Clown(user);
                break;
            case "Usurpation":
                Usurp(user);
                break;
            case "Perpetuation":
                Immortality(user);
                break;
            case "Cheese":
                Cheese(user);
                break;
        }
    }

    /// <summary>create_vendetta(): обиженный получает цель убить мага.</summary>
    private void CreateVendetta(EntityUid victim, EntityUid wizard)
    {
        if (!_mind.TryGetMind(victim, out var mindId, out var mind) || !_mind.TryGetMind(wizard, out var wizardMind, out _))
            return;

        if (_objectives.TryCreateObjective(mindId, mind, VendettaObjective) is not { } objective)
            return;

        _targets.SetTarget(objective, wizardMind);
        _meta.SetEntityName(objective, Loc.GetString("imperial-grand-finale-vendetta", ("wizard", Name(wizard))));
        _mind.AddObjective(mindId, mind, objective);
        _popup.PopupEntity(Loc.GetString("imperial-grand-finale-vendetta-popup"), victim, victim, PopupType.LargeCaution);
    }

    private IEnumerable<EntityUid> CrewWithMinds(EntityUid exclude)
    {
        var query = EntityQueryEnumerator<HumanoidProfileComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (uid != exclude && _mind.TryGetMind(uid, out _, out _))
                yield return uid;
        }
    }

    private bool HasJob(EntityUid body, ProtoId<Content.Shared.Roles.JobPrototype> job)
    {
        return _mind.TryGetMind(body, out var mindId, out _) && _jobs.MindTryGetJobId(mindId, out var id) && id == job;
    }

    /// <summary>all_access: все двери станции открыты и без доступа.</summary>
    private void AllAccess(EntityUid invoker)
    {
        var query = EntityQueryEnumerator<DoorComponent>();
        var doors = new List<(EntityUid, DoorComponent)>();
        while (query.MoveNext(out var uid, out var door))
        {
            if (_station.GetOwningStation(uid) != null)
                doors.Add((uid, door));
        }

        foreach (var (uid, door) in doors)
        {
            if (TryComp<AccessReaderComponent>(uid, out var reader))
                _access.SetActive((uid, reader), false);
            if (TryComp<DoorBoltComponent>(uid, out var bolts))
                _doors.SetBoltsDown((uid, bolts), false);
            _doors.TryOpen(uid, door);
        }

        Announce("AULIE OXIN FIERA!!", Name(invoker), new SoundPathSpecifier("/Audio/Imperial/Wizard/knock.ogg"), Color.Purple);
    }

    /// <summary>armageddon: маг гибнет, на его месте — сингулярность, тесла или метеоры.</summary>
    private void Armageddon(EntityUid invoker)
    {
        Announce(_random.Pick(LastWords), Name(invoker), new SoundPathSpecifier("/Audio/Imperial/Wizard/voidblink.ogg"), Color.Purple);
        var location = _transform.GetMapCoordinates(invoker);
        _gibbing.Gib(invoker);
        switch (_random.Next(3))
        {
            case 0:
                Spawn("Singularity", location);
                break;
            case 1:
                Spawn("TeslaEnergyBall", location);
                break;
            default:
                _ticker.StartGameRule("MeteorSwarmScheduler");
                Announce(Loc.GetString("imperial-grand-finale-meteors"), Loc.GetString("imperial-grand-finale-meteors-sender"));
                break;
        }
    }

    /// <summary>midas: станция превращается в золото, мясо или природу — по клетке в 5 секунд от мага.</summary>
    private void Midas(EntityUid invoker)
    {
        var theme = _random.Pick(MidasThemes);
        var center = Transform(invoker).Coordinates;
        var origin = _transform.GetWorldPosition(invoker);
        var invokerGrid = Transform(invoker).GridUid;
        var grids = new HashSet<EntityUid>();
        var query = EntityQueryEnumerator<Robust.Shared.Map.Components.MapGridComponent>();
        while (query.MoveNext(out var gridUid, out _))
        {
            if (_station.GetOwningStation(gridUid) != null)
                grids.Add(gridUid);
        }

        foreach (var gridUid in grids)
        {
            var grid = Comp<Robust.Shared.Map.Components.MapGridComponent>(gridUid);
            var rings = new Dictionary<int, List<Vector2i>>();
            foreach (var tile in _map.GetAllTiles(gridUid, grid))
            {
                var world = _map.GridTileToWorldPos(gridUid, grid, tile.GridIndices);
                var dist = (int) Math.Max(Math.Abs(world.X - origin.X), Math.Abs(world.Y - origin.Y));
                if (gridUid != invokerGrid)
                    dist = Math.Max(dist, 1);
                if (!rings.TryGetValue(dist, out var ring))
                    rings[dist] = ring = new List<Vector2i>();
                ring.Add(tile.GridIndices);
            }

            foreach (var (dist, tiles) in rings)
            {
                var g = gridUid;
                Schedule(TimeSpan.FromSeconds(5) * dist, () =>
                {
                    if (TerminatingOrDeleted(g))
                        return;
                    foreach (var tile in tiles)
                    {
                        ApplyTheme(theme, g, grid, tile);
                    }
                });
            }
        }
    }

    /// <summary>clown: весь экипаж «всегда работал» на клоунской станции.</summary>
    private void Clown(EntityUid invoker)
    {
        foreach (var victim in CrewWithMinds(EntityUid.Invalid).ToList())
        {
            _stun.TryUpdateParalyzeDuration(victim, TimeSpan.FromSeconds(3));
            if (victim == invoker)
            {
                // untie_shoes на максимум, выкрикивается.
                if (_spellSystem.FindSpell(invoker, "ImperialSpellTieShoes") != null)
                    continue;

                if (_spellbook.GrantSpell(invoker, "ImperialSpellTieShoes") is { } spell && TryComp<ImperialSpellComponent>(spell, out var comp))
                {
                    while (_spellSystem.LevelSpell((spell, comp)))
                    {
                    }

                    comp.InvocationType = ImperialInvocationType.Shout;
                }

                continue;
            }

            _popup.PopupEntity(Loc.GetString("imperial-grand-finale-clown-popup"), victim, victim, PopupType.Large);
            if (HasJob(victim, ClownJob))
            {
                _spellbook.GrantSpell(victim, "ImperialSpellTieShoes");
                continue;
            }

            DressAsClown(victim);
            if (_random.Prob(0.15f))
                CreateVendetta(victim, invoker);
        }
    }

    /// <summary>dress_as_magic_clown(): комбинезон и маска клоуна, карманы и пояс возвращаются на место.</summary>
    private void DressAsClown(EntityUid victim)
    {
        Spawn("WizardSmoke", _transform.GetMapCoordinates(victim));
        var kept = new List<(string Slot, EntityUid Item)>();
        foreach (var slot in new[] { "pocket1", "pocket2", "id", "belt" })
        {
            if (_inventory.TryGetSlotEntity(victim, slot, out var item) && _inventory.TryUnequip(victim, slot, true, true))
                kept.Add((slot, item.Value));
        }

        foreach (var slot in new[] { "jumpsuit", "mask" })
        {
            if (_inventory.TryGetSlotEntity(victim, slot, out var old))
            {
                _inventory.TryUnequip(victim, slot, true, true);
                QueueDel(old.Value);
            }
        }

        EquipNew(victim, "jumpsuit", "ClothingUniformJumpsuitClown");
        EquipNew(victim, "mask", "ClothingMaskClown");
        foreach (var (slot, item) in kept)
        {
            if (!_inventory.TryEquip(victim, item, slot, true, true))
                _hands.TryPickupAnyHand(victim, item);
        }
    }

    private void EquipNew(EntityUid target, string slot, EntProtoId proto)
    {
        var item = Spawn(proto, Transform(target).Coordinates);
        if (!_inventory.TryEquip(target, item, slot, true, true) && !_hands.TryPickupAnyHand(target, item))
            return;
    }

    /// <summary>usurp: маг всегда был капитаном этой станции.</summary>
    private void Usurp(EntityUid invoker)
    {
        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/Wizard/timeparadox2.ogg"), Filter.Broadcast(), true);
        var formerCaptains = new List<EntityUid>();
        var otherCrew = new List<EntityUid>();
        foreach (var crew in CrewWithMinds(EntityUid.Invalid).ToList())
        {
            _stun.TryUpdateParalyzeDuration(crew, TimeSpan.FromSeconds(3));
            if (crew == invoker)
                continue;

            _popup.PopupEntity(Loc.GetString("imperial-grand-finale-usurp-popup", ("wizard", Name(invoker))), crew, crew, PopupType.Large);
            if (HasJob(crew, CaptainJob))
            {
                formerCaptains.Add(crew);
                DemoteToAssistant(crew);
                continue;
            }

            if (!_mobs.IsDead(crew))
                otherCrew.Add(crew);
        }

        DressCandidate(invoker);
        Announce(Loc.GetString("imperial-grand-finale-usurp-announce", ("wizard", Name(invoker))));
        foreach (var captain in formerCaptains)
        {
            CreateVendetta(captain, invoker);
        }

        foreach (var crew in otherCrew)
        {
            if (_random.Prob(0.1f))
                CreateVendetta(crew, invoker);
        }
    }

    /// <summary>demote_to_assistant(): бывший капитан раздет, одет пассажиром и отправлен куда-то на станцию.</summary>
    private void DemoteToAssistant(EntityUid captain)
    {
        Spawn("WizardSmoke", _transform.GetMapCoordinates(captain));
        if (_inventory.TryGetContainerSlotEnumerator(captain, out var slots))
        {
            while (slots.NextItem(out var item, out var slot))
            {
                _inventory.TryUnequip(captain, slot.Name, true, true);
                QueueDel(item);
            }
        }

        if (_proto.TryIndex(PassengerGear, out var gear))
            EntityManager.System<Content.Shared.Station.SharedStationSpawningSystem>().EquipStartingGear(captain, gear);

        var areas = _teleport.GetAreas().Values.ToList();
        if (areas.Count > 0 && _teleport.TryGetDestination(_random.Pick(areas), out var dest))
            _transform.SetCoordinates(captain, dest);
        Spawn("WizardSmoke", _transform.GetMapCoordinates(captain));
    }

    /// <summary>dress_candidate(): карта, гарнитура и форма капитана, мантия остаётся.</summary>
    private void DressCandidate(EntityUid invoker)
    {
        var kept = new List<(string Slot, EntityUid Item)>();
        foreach (var slot in new[] { "pocket1", "pocket2", "belt" })
        {
            if (_inventory.TryGetSlotEntity(invoker, slot, out var item) && _inventory.TryUnequip(invoker, slot, true, true))
                kept.Add((slot, item.Value));
        }

        foreach (var slot in new[] { "id", "ears", "jumpsuit" })
        {
            if (_inventory.TryGetSlotEntity(invoker, slot, out var old))
            {
                _inventory.TryUnequip(invoker, slot, true, true);
                QueueDel(old.Value);
            }
        }

        EquipNew(invoker, "jumpsuit", "ClothingUniformJumpsuitCaptain");
        EquipNew(invoker, "id", "CaptainIDCard");
        EquipNew(invoker, "ears", "ClothingHeadsetAltCommand");
        foreach (var (slot, proto) in new[] { ("eyes", "ClothingEyesGlassesSunglasses"), ("gloves", "ClothingHandsGlovesCaptain"), ("neck", "ClothingNeckGoldmedal") })
        {
            if (!_inventory.TryGetSlotEntity(invoker, slot, out _))
                EquipNew(invoker, slot, proto);
        }

        var baton = Spawn("TelescopicBaton", Transform(invoker).Coordinates);
        _hands.TryPickupAnyHand(invoker, baton);
        foreach (var (slot, item) in kept)
        {
            if (!_inventory.TryEquip(invoker, item, slot, true, true))
                _hands.TryPickupAnyHand(invoker, item);
        }
    }

    /// <summary>immortality: никто больше не умирает навсегда.</summary>
    private void Immortality(EntityUid invoker)
    {
        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/Wizard/teleport_diss.ogg"), Filter.Broadcast(), true);
        var query = EntityQueryEnumerator<MobStateComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            Spawn("ImperialImmortalityPulse", _transform.GetMapCoordinates(uid));
            if (_mind.TryGetMind(uid, out _, out _))
                _popup.PopupEntity(Loc.GetString("imperial-grand-finale-immortal-popup"), uid, uid, PopupType.Large);
        }

        _immortality = true;
    }

    /// <summary>something_died(): через 10 секунд тело уходит к призраку, через 50 — возвращается к жизни.</summary>
    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (!_immortality || args.NewMobState != MobState.Dead || HasComp<ImperialWormSegmentComponent>(args.Target))
            return;

        var died = args.Target;
        if (_mind.TryGetMind(died, out _, out _))
            _popup.PopupEntity(Loc.GetString("imperial-grand-finale-immortal-surge"), died, died, PopupType.Large);

        Schedule(TimeSpan.FromSeconds(10), () =>
        {
            if (TerminatingOrDeleted(died) || !_mobs.IsDead(died) || _containers.IsEntityInContainer(died))
                return;

            var spectre = Spawn("ImperialSpectreOfResurrection", _transform.GetMapCoordinates(died));
            _meta.SetEntityName(spectre, Loc.GetString("imperial-grand-finale-spectre-name", ("name", Name(died))));
            var container = _containers.EnsureContainer<ContainerSlot>(spectre, "corpse");
            _containers.Insert(died, container, force: true);
            var comp = EnsureComp<ImperialSpectreOfResurrectionComponent>(spectre);
            comp.Corpse = died;
            Schedule(TimeSpan.FromSeconds(50), () => Revive(spectre));
        });
    }

    /// <summary>spectre/revive().</summary>
    private void Revive(EntityUid spectre)
    {
        if (TerminatingOrDeleted(spectre) || !TryComp<ImperialSpectreOfResurrectionComponent>(spectre, out var comp))
            return;

        if (comp.Corpse is { } corpse && !TerminatingOrDeleted(corpse))
        {
            _containers.TryRemoveFromContainer(corpse, true);
            _transform.SetMapCoordinates(corpse, _transform.GetMapCoordinates(spectre));
            RaiseLocalEvent(corpse, new RejuvenateEvent());
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Wizard/staff_healing.ogg"), corpse);
        }

        QueueDel(spectre);
    }

    /// <summary>cheese: Вабаджак магу, безумие и вендетта — всем остальным.</summary>
    private void Cheese(EntityUid invoker)
    {
        Announce(Loc.GetString("imperial-grand-finale-cheese-announce"), Loc.GetString("imperial-grand-finale-cheese-sender"),
            new SoundPathSpecifier("/Audio/Effects/glass_break1.ogg"));
        var trait = _proto.Index(Hallucinations);
        foreach (var crew in CrewWithMinds(invoker).ToList())
        {
            if (HasComp<Content.Shared.Traits.Assorted.ParacusiaComponent>(crew))
            {
                // Для уже безумного это искупление.
                RemComp<Content.Shared.Traits.Assorted.ParacusiaComponent>(crew);
                _popup.PopupEntity(Loc.GetString("imperial-grand-finale-cheese-cured"), crew, crew, PopupType.Large);
                continue;
            }

            CreateVendetta(crew, invoker);
            _popup.PopupEntity(Loc.GetString("imperial-grand-finale-cheese-madness", ("wizard", Name(invoker))), crew, crew, PopupType.LargeCaution);
#pragma warning disable CS0618
            EntityManager.AddComponents(crew, trait.Components, false);
#pragma warning restore CS0618
        }

        var wabbajack = Spawn("ImperialTrueWabbajack", Transform(invoker).Coordinates);
        _hands.TryPickupAnyHand(invoker, wabbajack);
        _popup.PopupEntity(Loc.GetString("imperial-grand-finale-cheese-wabbajack", ("item", wabbajack)), invoker, invoker, PopupType.Large);
    }
}

using Content.Shared.Stacks;
using Robust.Shared.Map.Components;
using Content.Shared.Random.Helpers;
using Content.Shared.Prototypes;
using Content.Shared.Coordinates.Helpers;
using Robust.Shared.Random;
using System.Linq;
using Content.Server.Decals;
using Content.Server.Imperial.Xenobiology.Components;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Decals;
using Content.Shared.DoAfter;
using Content.Shared.Flash;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Xenobiology;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Power.Components;
using Content.Shared.Administration.Systems;
using Content.Shared.Examine;
using Content.Shared.Storage;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Xenobiology;

public sealed partial class SlimeCrossbreedSystem
{
    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly EntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedFlashSystem _flash = default!;
    [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;

    private static readonly ProtoId<TagPrototype> MonkeyCubeTag = "MonkeyCube";
    private static readonly ProtoId<DecalPrototype> PaintDecal = "FullTileOverlayGreyscale";
    private static readonly string[] Coins = { "TreasureCoinGold", "TreasureCoinSilver", "TreasureCoinIron", "TreasureCoinDiamond", "TreasureCoinAdamantine" };

    private void InitializePassive()
    {
        SubscribeLocalEvent<SlimeIndustrialComponent, ExaminedEvent>(OnIndustrialExamined);
        SubscribeLocalEvent<SlimeRecurringComponent, MapInitEvent>(OnRecurringInit);
        SubscribeLocalEvent<SlimeSelfSustainingComponent, MapInitEvent>(OnSelfSustainingInit);
        SubscribeLocalEvent<SlimeAutoslimeComponent, GetVerbsEvent<ActivationVerb>>(OnAutoslimeVerbs);
        SubscribeLocalEvent<SlimeReproductiveComponent, InteractUsingEvent>(OnReproductiveInteract);
        SubscribeLocalEvent<SlimeReproductiveComponent, ExaminedEvent>(OnReproductiveExamined);
        SubscribeLocalEvent<SlimePrismaticComponent, AfterInteractEvent>(OnPrismaticInteract);
        SubscribeLocalEvent<SlimeRegenerativeComponent, MapInitEvent>(OnRegenerativeInit);
        SubscribeLocalEvent<SlimeRegenerativeComponent, AfterInteractEvent>(OnRegenerativeInteract);
        SubscribeLocalEvent<SlimeConsumingComponent, InteractUsingEvent>(OnConsumingInteract);
        SubscribeLocalEvent<SlimeCookieComponent, MapInitEvent>(OnCookieInit);
        SubscribeLocalEvent<SlimeCookieComponent, AfterInteractEvent>(OnCookieInteract);
        SubscribeLocalEvent<SlimeCookieComponent, UseInHandEvent>(OnCookieUse);
        SubscribeLocalEvent<SlimeCookieComponent, SlimeCookieFeedDoAfterEvent>(OnCookieDoAfter);
        SubscribeLocalEvent<SlimeCrossLinkComponent, SlimeCrossRecallDoAfterEvent>(OnRecallDoAfter);
    }

    private void UpdatePassive(TimeSpan now, float frameTime)
    {
        UpdateIndustrial(now);
        UpdateRecurring(frameTime);
        UpdateChargedGold(now);
    }

    #region Industrial

    private void OnIndustrialExamined(Entity<SlimeIndustrialComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("slime-cross-industrial-examine", ("absorbed", ent.Comp.Absorbed), ("required", ent.Comp.PlasmaRequired)));
    }

    /// <summary>industrial/process(): 2u плазмы за тик SSobj (2 с).</summary>
    private void UpdateIndustrial(TimeSpan now)
    {
        var query = EntityQueryEnumerator<SlimeIndustrialComponent>();
        while (query.MoveNext(out var uid, out var industrial))
        {
            if (now < industrial.NextTick)
                continue;
            industrial.NextTick = now + TimeSpan.FromSeconds(2);

            var working = false;
            if (_solutions.TryGetSolution(uid, industrial.Solution, out var solutionEnt, out var solution))
            {
                var plasma = solution.GetTotalPrototypeQuantity(Plasma);
                if (plasma >= 2 && industrial.PlasmaRequired > 1)
                {
                    _solutions.RemoveReagent(solutionEnt.Value, Plasma, 2);
                    industrial.Absorbed += 2;
                    working = true;
                }
                else if (plasma >= 1)
                {
                    _solutions.RemoveReagent(solutionEnt.Value, Plasma, 1);
                    industrial.Absorbed += 1;
                    working = true;
                }
            }

            if (industrial.Absorbed >= industrial.PlasmaRequired)
            {
                _audio.PlayPvs(AttackBlob, uid);
                industrial.Absorbed -= industrial.PlasmaRequired;
                var coords = At(uid);
                for (var i = 0; i < industrial.Amount; i++)
                {
                    if (IndustrialProduct(industrial) is not { } proto)
                        continue;
                    var item = Spawn(proto, coords);
                    if (industrial.StackCount > 0 && TryComp<StackComponent>(item, out var stack))
                        _stack.SetCount((item, stack), industrial.StackCount);
                    IndustrialAfterSpawn(industrial, item);
                }
            }
            else if (working)
            {
                _audio.PlayPvs(Bubbles, uid, Robust.Shared.Audio.AudioParams.Default.WithVolume(-12f));
            }

            if (industrial.Working != working)
            {
                industrial.Working = working;
                _appearance.SetData(uid, SlimeIndustrialVisuals.Working, working);
            }
        }
    }

    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    private EntProtoId? IndustrialProduct(SlimeIndustrialComponent industrial)
    {
        switch (industrial.RandomPool)
        {
            case "foodordrink":
            {
                var pool = _random.Prob(0.5f) ? _extract.Foods() : _extract.Drinks();
                return pool.Count > 0 ? _random.Pick(pool) : (EntProtoId?) null;
            }
            case "coin":
                return _random.Pick(Coins);
            case "extract":
            {
                var extracts = _proto.EnumeratePrototypes<EntityPrototype>()
                    .Where(p => !p.Abstract && p.ID.StartsWith("SlimeExtract") && p.HasComponent<SlimeExtractComponent>(EntityManager.ComponentFactory))
                    .Select(p => p.ID).ToList();
                return extracts.Count > 0 ? new EntProtoId(_random.Pick(extracts)) : (EntProtoId?) null;
            }
            default:
                return industrial.Item;
        }
    }

    private void IndustrialAfterSpawn(SlimeIndustrialComponent industrial, EntityUid item)
    {
        switch (industrial.AfterSpawn)
        {
            case "oneuse" when TryComp<SlimePotionComponent>(item, out var potion):
                potion.Uses = 1;
                Dirty(item, potion);
                break;
            case "halfcharge" when TryComp<BatteryComponent>(item, out var battery):
                _battery.SetCharge((item, battery), _random.NextFloat(0f, battery.MaxCharge / 2f));
                break;
        }
    }

    #endregion

    #region Recurring

    private void OnRecurringInit(Entity<SlimeRecurringComponent> ent, ref MapInitEvent args)
    {
        if (TryComp<SlimeExtractComponent>(ent, out var extract))
        {
            extract.Recurring = true;
            Dirty(ent, extract);
        }

        Popup("slime-cross-recurring-wrap", ent, ("extract", ent.Owner));
    }

    /// <summary>recurring/process(): новый заряд каждые max_cooldown секунд, максимум 10.</summary>
    private void UpdateRecurring(float frameTime)
    {
        var query = EntityQueryEnumerator<SlimeRecurringComponent, SlimeExtractComponent>();
        while (query.MoveNext(out var uid, out var recurring, out var extract))
        {
            if (recurring.Cooldown > 0f)
            {
                recurring.Cooldown -= frameTime;
                continue;
            }

            if (extract.Uses < 10 && extract.Uses > 0)
            {
                extract.Uses++;
                Dirty(uid, extract);
                recurring.Cooldown = recurring.MaxCooldown;
            }
            else if (extract.Uses <= 0)
            {
                Popup("slime-cross-recurring-dead", uid, ("extract", uid));
                RemCompDeferred<SlimeRecurringComponent>(uid);
            }
        }
    }

    #endregion

    #region Self-sustaining

    /// <summary>selfsustaining/Initialize(): распадается на четыре автослайма.</summary>
    private void OnSelfSustainingInit(Entity<SlimeSelfSustainingComponent> ent, ref MapInitEvent args)
    {
        var coords = At(ent);
        Popup("slime-cross-selfsustaining-split", ent, ("extract", ent.Owner));
        var colourName = TryComp<SlimeCrossbreedComponent>(ent, out var cross) && _proto.TryIndex(cross.SlimeType, out var type)
            ? Loc.GetString(type.Name)
            : string.Empty;

        for (var i = 0; i < 4; i++)
        {
            var auto = Spawn(ent.Comp.Autoslime, coords);
            EnsureComp<SlimeAutoslimeComponent>(auto).Extract = ent.Comp.Extract;
            _meta.SetEntityName(auto, Loc.GetString("slime-cross-autoslime-name", ("colour", colourName)));
        }

        QueueDel(ent);
    }

    /// <summary>autoslime/attack_self(): выбор реакции из белого списка.</summary>
    private void OnAutoslimeVerbs(Entity<SlimeAutoslimeComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Extract is not { } extractProto)
            return;
        if (!_proto.TryIndex(extractProto, out var proto) || !proto.TryGetComponent<SlimeExtractComponent>(out var extractComp, EntityManager.ComponentFactory))
            return;

        var user = args.User;
        foreach (var reaction in _extract.AutoActivateReactions(extractComp.SlimeType))
        {
            var names = string.Join(", ", reaction.Reactants.Keys.Select(r => _proto.Index(r).LocalizedName));
            args.Verbs.Add(new ActivationVerb
            {
                Text = Loc.GetString("slime-cross-autoslime-verb", ("reagents", names)),
                Category = VerbCategory.SelectType,
                Act = () => ActivateAutoslime(ent, user, extractProto, reaction),
            });
        }
    }

    private void ActivateAutoslime(Entity<SlimeAutoslimeComponent> ent, EntityUid user, EntProtoId extractProto, SlimeReactionPrototype reaction)
    {
        if (TerminatingOrDeleted(ent) || !_hands.IsHolding(user, ent))
            return;

        QueueDel(ent);
        var extract = SpawnForUser(extractProto, user);
        if (TryComp<SlimeExtractComponent>(extract, out var extractComp))
            _extract.AutoActivate((extract, extractComp), reaction);
    }

    #endregion

    #region Reproductive

    private void OnReproductiveExamined(Entity<SlimeReproductiveComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("slime-cross-reproductive-examine", ("count", ent.Comp.Cubes)));
    }

    private void OnReproductiveInteract(Entity<SlimeReproductiveComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var cubes = new List<EntityUid>();
        if (_tag.HasTag(args.Used, MonkeyCubeTag))
            cubes.Add(args.Used);
        else if (HasComp<SlimeExtractBagComponent>(args.Used) && TryComp<StorageComponent>(args.Used, out var storage))
            cubes.AddRange(storage.Container.ContainedEntities.Where(e => _tag.HasTag(e, MonkeyCubeTag)));
        else
            return;

        args.Handled = true;
        if (ent.Comp.LastProduce + ent.Comp.Cooldown > _timing.CurTime)
        {
            PopupUser("slime-cross-reproductive-digesting", args.User, ("extract", ent.Owner));
            return;
        }

        if (cubes.Count == 0)
        {
            PopupUser("slime-cross-reproductive-no-cubes", args.User);
            return;
        }

        var fed = 0;
        foreach (var cube in cubes)
        {
            if (ent.Comp.Cubes >= ent.Comp.FeedAmount)
                break;
            ent.Comp.Cubes++;
            fed++;
            QueueDel(cube);
        }

        PopupUser("slime-cross-reproductive-fed", args.User, ("count", fed), ("extract", ent.Owner));
        _audio.PlayPvs(EatFood, ent);

        // processCubes(): после трёх кубиков рождаются 1–4 экстракта.
        if (ent.Comp.Cubes < ent.Comp.FeedAmount)
            return;

        ent.Comp.Cubes = 0;
        ent.Comp.LastProduce = _timing.CurTime;
        var amount = _random.Next(1, 5);
        for (var i = 0; i < amount; i++)
        {
            Spawn(ent.Comp.Extract, At(ent));
        }
        Popup("slime-cross-reproductive-produce", ent, ("extract", ent.Owner));
    }

    #endregion

    #region Prismatic

    /// <summary>prismatic/interact_with_atom(): красит пол (декалью на весь тайл).</summary>
    private void OnPrismaticInteract(Entity<SlimePrismaticComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target != null)
            return;

        var coords = args.ClickLocation.SnapToGrid(EntityManager);
        if (Transform(coords.EntityId).GridUid is not { } grid)
            return;

        args.Handled = true;
        var decalCoords = coords.Offset(new System.Numerics.Vector2(-0.5f, -0.5f));
        foreach (var (id, decal) in _decals.GetDecalsInRange(grid, _transform.ToMapCoordinates(coords).Position, 0.5f).ToList())
        {
            if (decal.Id == PaintDecal.Id)
                _decals.RemoveDecal(grid, id);
        }

        if (!ent.Comp.Remover)
            _decals.TryAddDecal(PaintDecal.Id, decalCoords, out _, ent.Comp.PaintColor.WithAlpha(0.5f), cleanable: true);

        _audio.PlayPvs(new Robust.Shared.Audio.SoundPathSpecifier("/Audio/Effects/Fluids/slosh.ogg"), coords);
    }

    #endregion

    #region Regenerative

    private void OnRegenerativeInit(Entity<SlimeRegenerativeComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.Origin = At(ent);
    }

    /// <summary>regenerative/interact_with_atom(): лечит цель и даёт эффект цвета.</summary>
    private void OnRegenerativeInteract(Entity<SlimeRegenerativeComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<MobStateComponent>(target))
            return;
        args.Handled = true;
        UseRegenerative(ent, target, args.User);
    }

    public void UseRegenerative(Entity<SlimeRegenerativeComponent> ent, EntityUid target, EntityUid user)
    {
        if (_mobState.IsDead(target))
        {
            PopupUser("slime-cross-regenerative-dead", user, ("extract", ent.Owner));
            return;
        }

        var colour = Colour(ent);
        Popup(target == user ? "slime-cross-regenerative-self" : "slime-cross-regenerative-other", target,
            ("user", user), ("target", target), ("extract", ent.Owner));

        RegenerativeBefore(colour, target, user);

        // helbital 3, lenturi 4, tirimol 5, multiver 15.
        Heal(target, 25f, 25f, 25f, 15f);

        RegenerativeAfter(ent, colour, target, user);
        _audio.PlayPvs(Splat, target);
        QueueDel(ent);
    }

    private void RegenerativeBefore(string colour, EntityUid target, EntityUid user)
    {
        switch (colour)
        {
            case "SlimeOrange":
                Popup("slime-cross-regenerative-orange", target);
                foreach (var tile in TilesAround(At(target), 1))
                {
                    Hotspot(tile);
                }
                break;
            case "SlimeSepia":
                PopupUser("slime-cross-regenerative-sepia", target);
                StartDejavu(target, 1);
                break;
            case "SlimeBlack":
                MakeDummyCorpse(target, user);
                break;
        }
    }

    private void RegenerativeAfter(Entity<SlimeRegenerativeComponent> ent, string colour, EntityUid target, EntityUid user)
    {
        var coords = At(target);
        switch (colour)
        {
            case "SlimePurple":
                InjectReagent(target, "RegenJelly", 10f);
                break;
            case "SlimeBlue":
                _puddle.TrySpillAt(coords, new Solution("Water", 15), out _, sound: false);
                Popup("slime-cross-regenerative-blue", target);
                break;
            case "SlimeMetal":
            {
                Popup("slime-cross-regenerative-metal", target, ("target", target));
                var closet = Spawn("ClosetSteelBase", coords);
                _meta.SetEntityName(closet, Loc.GetString("slime-cross-slimy-closet"));
                if (!_entityStorage.Insert(target, closet))
                    _entityStorage.OpenStorage(closet);
                break;
            }
            case "SlimeYellow":
                if (ChargeOneBattery(target, 1f))
                    PopupUser("slime-cross-regenerative-yellow", target);
                break;
            case "SlimeDarkPurple":
            {
                var equipped = false;
                equipped |= EquipIfEmpty(target, "ClothingShoesColorPurple", "shoes");
                equipped |= EquipIfEmpty(target, "ClothingUniformJumpsuitColorPurple", "jumpsuit");
                equipped |= EquipIfEmpty(target, "ClothingHandsGlovesColorPurple", "gloves");
                equipped |= EquipIfEmpty(target, "ClothingHeadHatPurplesoft", "head");
                if (equipped)
                    Popup("slime-cross-regenerative-darkpurple", target);
                break;
            }
            case "SlimeDarkBlue":
            {
                var any = false;
                foreach (var slot in new[] { "outerClothing", "head" })
                {
                    if (!_inventory.TryGetSlotEntity(target, slot, out var clothing))
                        continue;
                    var potioned = EnsureComp<SlimePotionedComponent>(clothing.Value);
                    if (!potioned.Fireproof)
                    {
                        potioned.Fireproof = true;
                        Dirty(clothing.Value, potioned);
                        _meta.SetEntityName(clothing.Value, Loc.GetString("slime-potion-fireproofed-name", ("name", Name(clothing.Value))));
                    }
                    any = true;
                }
                if (any)
                    Popup("slime-cross-regenerative-darkblue", target, ("target", target));
                break;
            }
            case "SlimeSilver":
                if (TryComp<HungerComponent>(target, out var hunger))
                    _hunger.SetHunger(target, hunger.Thresholds[Content.Shared.Nutrition.Components.HungerThreshold.Overfed] - 1f, hunger);
                PopupUser("slime-cross-regenerative-silver", target);
                break;
            case "SlimeBluespace":
                if (ent.Comp.Origin is { } origin && origin.IsValid(EntityManager))
                {
                    Popup("slime-cross-regenerative-bluespace", target, ("target", target));
                    _transform.SetCoordinates(target, origin);
                    _transform.AttachToGridOrMap(target);
                    PopupUser("slime-cross-regenerative-bluespace-self", target);
                }
                break;
            case "SlimeCerulean":
            {
                var copy = Spawn("SlimeCrossRegenerativeGrey", At(user));
                _meta.SetEntityName(copy, Name(ent));
                _hands.TryPickupAnyHand(user, copy);
                PopupUser("slime-cross-regenerative-cerulean", user);
                break;
            }
            case "SlimePyrite":
                Popup("slime-cross-regenerative-pyrite", target, ("target", target));
                SetTint(target, "pyrite", new Color(_random.NextFloat(), _random.NextFloat(), _random.NextFloat()));
                break;
            case "SlimeRed":
                PopupUser("slime-cross-regenerative-red", target);
                InjectReagent(target, "Ephedrine", 3f);
                break;
            case "SlimeGreen":
                if (TryComp<SlimeComponent>(target, out var slime))
                {
                    Popup("slime-cross-regenerative-green", target, ("target", target));
                    _slime.SetSlimeType((target, slime), _slime.RandomSlimeType());
                }
                else if (TryComp<HumanoidProfileComponent>(target, out var profile) && profile.Species == "SlimePerson")
                {
                    InjectReagent(target, "MutationToxinJelly", 5f);
                }
                break;
            case "SlimePink":
                PopupUser("slime-cross-regenerative-pink", target);
                InjectReagent(target, "SpaceDrugs", 4f);
                break;
            case "SlimeGold":
            {
                var coin = Spawn(_random.Pick(Coins), coords);
                _hands.TryPickupAnyHand(target, coin);
                break;
            }
            case "SlimeOil":
                _flash.FlashArea(user, user, 7f, TimeSpan.FromSeconds(4));
                break;
            case "SlimeLightPink":
                if (target != user)
                {
                    _rejuvenate.PerformRejuvenate(user);
                    PopupUser("slime-cross-regenerative-lightpink", user);
                }
                break;
            case "SlimeAdamantine":
                Popup("slime-cross-slimeskin-on", target, ("target", target));
                AddEffect(target, "slimeskin", TimeSpan.FromSeconds(30));
                break;
            case "SlimeRainbow":
                Popup("slime-cross-rainbow-on", target, ("target", target));
                AddEffect(target, "rainbow", TimeSpan.FromSeconds(10));
                break;
        }
    }

    private bool EquipIfEmpty(EntityUid target, string proto, string slot)
    {
        if (_inventory.TryGetSlotEntity(target, slot, out _))
            return false;
        var item = Spawn(proto, At(target));
        if (_inventory.TryEquip(target, item, slot, silent: true, force: true))
            return true;
        QueueDel(item);
        return false;
    }

    /// <summary>Заряжает одну неполную батарею существа на долю ёмкости.</summary>
    private bool ChargeOneBattery(EntityUid owner, float fraction)
    {
        var batteries = new List<(EntityUid, BatteryComponent)>();
        CollectBatteries(owner, batteries, 0);
        if (batteries.Count == 0)
            return false;

        var (uid, battery) = _random.Pick(batteries);
        _battery.ChangeCharge((uid, battery), battery.MaxCharge * fraction);
        return true;
    }

    private void CollectBatteries(EntityUid uid, List<(EntityUid, BatteryComponent)> list, int depth)
    {
        if (depth > 4 || !TryComp<Robust.Shared.Containers.ContainerManagerComponent>(uid, out var manager))
            return;
        foreach (var container in manager.Containers.Values)
        {
            foreach (var child in container.ContainedEntities)
            {
                if (TryComp<BatteryComponent>(child, out var battery) && _battery.GetCharge((child, battery)) < battery.MaxCharge)
                    list.Add((child, battery));
                CollectBatteries(child, list, depth + 1);
            }
        }
    }

    /// <summary>regenerative/black: неидеальная копия цели инсценирует её смерть.</summary>
    private void MakeDummyCorpse(EntityUid target, EntityUid user)
    {
        var proto = MetaData(target).EntityPrototype?.ID;
        if (proto == null || !HasComp<HumanoidProfileComponent>(target))
        {
            proto = "MobXenoSlime";
            PopupUser("slime-cross-regenerative-black-puddle", user, ("target", target));
        }

        var dummy = Spawn(proto, At(target));
        _meta.SetEntityName(dummy, Name(target));
        if (TryComp<Content.Shared.Damage.Components.DamageableComponent>(target, out var damageable))
            _damageable.TryChangeDamage(dummy, new Content.Shared.Damage.DamageSpecifier(_damageable.GetAllDamage((target, damageable))), ignoreResistances: true);
        if (_thresholds.TryGetDeadThreshold(dummy, out var dead))
            Damage(dummy, "Blunt", dead.Value.Float());
        PopupUser("slime-cross-regenerative-black", target);
    }

    #endregion

    #region Consuming

    /// <summary>consuming/item_interaction(): поглощает еду с питательными веществами.</summary>
    private void OnConsumingInteract(Entity<SlimeConsumingComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<EdibleComponent>(args.Used))
            return;
        args.Handled = true;

        if (ent.Comp.LastProduced + ent.Comp.Cooldown > _timing.CurTime)
        {
            PopupUser("slime-cross-consuming-digesting", args.User, ("extract", ent.Owner));
            return;
        }

        var nutriment = 0f;
        if (_solutions.TryGetSolution(args.Used, "food", out _, out var food))
            nutriment = food.GetTotalPrototypeQuantity("Nutriment").Float();

        if (nutriment <= 0f)
        {
            PopupUser("slime-cross-consuming-unhappy", args.User, ("extract", ent.Owner));
            return;
        }

        ent.Comp.NutrimentEaten += nutriment;
        PopupUser("slime-cross-consuming-swallow", args.User, ("extract", ent.Owner), ("food", args.Used));
        QueueDel(args.Used);
        _audio.PlayPvs(EatFood, ent);

        if (ent.Comp.NutrimentEaten < ent.Comp.NutrimentRequired)
            return;

        ent.Comp.NutrimentEaten = 0f;
        ent.Comp.LastProduced = _timing.CurTime;
        Popup("slime-cross-consuming-cookies", ent, ("extract", ent.Owner));
        _audio.PlayPvs(Splat, ent);

        for (var i = 0; i < ent.Comp.Cookies; i++)
        {
            SpawnCookie(ent.Comp.Cookie, At(ent));
        }
    }

    private void SpawnCookie(EntProtoId? proto, EntityCoordinates coords)
    {
        var offset = new System.Numerics.Vector2(_random.NextFloat(-0.15f, 0.15f), _random.NextFloat(-0.15f, 0.15f));
        if (proto != null)
        {
            Spawn(proto.Value, coords.Offset(offset));
            return;
        }

        // consuming/rainbow: случайное печенье в радужной обёртке.
        var cookies = _proto.EnumeratePrototypes<EntityPrototype>()
            .Where(p => !p.Abstract && p.ID.StartsWith("SlimeCookie") && p.HasComponent<SlimeCookieComponent>(EntityManager.ComponentFactory) && p.ID != "SlimeCookieRainbow")
            .Select(p => p.ID).ToList();
        if (cookies.Count == 0)
            return;
        var cookie = Spawn(_random.Pick(cookies), coords.Offset(offset));
        _meta.SetEntityName(cookie, Loc.GetString("slime-cookie-rainbow-name"));
        _meta.SetEntityDescription(cookie, Loc.GetString("slime-cookie-rainbow-desc"));
    }

    private void OnCookieInit(Entity<SlimeCookieComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Effect != "SlimePyrite")
            return;

        // pyrite: случайный краситель.
        var dyes = new (string, Color)[]
        {
            ("red", Color.Red), ("orange", Color.FromHex("#FFA500")), ("yellow", Color.Yellow), ("green", Color.FromHex("#00FF00")),
            ("blue", Color.Blue), ("indigo", Color.FromHex("#4B0082")), ("violet", Color.Magenta),
        };
        var (name, color) = _random.Pick(dyes);
        ent.Comp.Dye = color;
        ent.Comp.Taste = $"slime-cookie-taste-pyrite-{name}";
    }

    private void OnCookieUse(Entity<SlimeCookieComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        EatCookie(ent, args.User, args.User);
    }

    private void OnCookieInteract(Entity<SlimeCookieComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<MobStateComponent>(target))
            return;
        args.Handled = true;

        if (target == args.User)
        {
            EatCookie(ent, target, args.User);
            return;
        }

        Popup("slime-cookie-force", target, ("user", args.User), ("target", target), ("cookie", ent.Owner));
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(2), new SlimeCookieFeedDoAfterEvent(), ent, target, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });
    }

    private void OnCookieDoAfter(Entity<SlimeCookieComponent> ent, ref SlimeCookieFeedDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;
        args.Handled = true;
        Popup("slime-cookie-forced", target, ("user", args.User), ("target", target), ("cookie", ent.Owner));
        EatCookie(ent, target, args.User);
    }

    /// <summary>slime_cookie: вкус, питание и эффект цвета.</summary>
    private void EatCookie(Entity<SlimeCookieComponent> ent, EntityUid eater, EntityUid user)
    {
        if (TerminatingOrDeleted(ent))
            return;

        PopupUser("slime-cookie-taste", eater, ("taste", Loc.GetString(ent.Comp.Taste)));
        _audio.PlayPvs(EatFood, eater);
        if (ent.Comp.Nutrition > 0f)
            InjectReagent(eater, "Nutriment", ent.Comp.Nutrition);

        CookieEffect(ent, eater);
        QueueDel(ent);
    }

    private void CookieEffect(Entity<SlimeCookieComponent> ent, EntityUid eater)
    {
        var coords = At(eater);
        switch (ent.Comp.Effect)
        {
            case "SlimeOrange":
                AddEffect(eater, "fire", TimeSpan.FromSeconds(10));
                if (TryComp<Content.Shared.Temperature.Components.TemperatureComponent>(eater, out var heat))
                    _temperature.ForceChangeTemperature(eater, heat.CurrentTemperature + 110f, heat);
                break;
            case "SlimePurple":
                Heal(eater, 5f, 5f, 5f, 5f);
                break;
            case "SlimeBlue":
                AddEffect(eater, "water", TimeSpan.FromSeconds(10));
                break;
            case "SlimeMetal":
                AddEffect(eater, "metal", TimeSpan.FromSeconds(10));
                break;
            case "SlimeYellow":
                AddEffect(eater, "spark", TimeSpan.FromSeconds(30));
                break;
            case "SlimeDarkPurple":
                AddEffect(eater, "toxinlover", TimeSpan.FromMinutes(1));
                break;
            case "SlimeDarkBlue":
                if (TryComp<Content.Shared.Temperature.Components.TemperatureComponent>(eater, out var cold))
                    _temperature.ForceChangeTemperature(eater, Math.Max(2.7f, cold.CurrentTemperature - 110f), cold);
                _flammable.Extinguish(eater);
                break;
            case "SlimeSilver":
                InjectReagent(eater, "Nutriment", 10f);
                break;
            case "SlimeBluespace":
                _items.BlinkMob(eater, 10);
                break;
            case "SlimeSepia":
                AddEffect(eater, "time", TimeSpan.FromMinutes(1));
                break;
            case "SlimeCerulean":
                if (_random.Prob(0.5f))
                {
                    PopupUser("slime-cookie-cerulean", eater, ("cookie", ent.Owner));
                    Spawn(MetaData(ent).EntityPrototype?.ID ?? "SlimeCookieCerulean", coords);
                }
                break;
            case "SlimePyrite":
                if (ent.Comp.Dye is { } dye)
                    SetTint(eater, "pyrite", dye);
                break;
            case "SlimeRed":
                _puddle.TrySpillAt(coords, new Solution("Blood", 5), out _, sound: false);
                _audio.PlayPvs(Splat, coords);
                _bloodstream.TryModifyBloodLevel(eater, 25);
                break;
            case "SlimeGreen":
                _vomit.Vomit(eater, force: true);
                if (TryComp<Content.Shared.Body.Components.BloodstreamComponent>(eater, out var stream)
                    && _solutions.TryGetSolution(eater, stream.BloodSolutionName, out var bloodEnt, out var blood))
                {
                    foreach (var reagent in blood.Contents.ToList())
                    {
                        if (reagent.Reagent.Prototype != "Blood")
                            _solutions.RemoveReagent(bloodEnt.Value, reagent);
                    }
                }
                break;
            case "SlimePink":
                AddEffect(eater, "love", TimeSpan.FromSeconds(30));
                break;
            case "SlimeGold":
            {
                var coin = Spawn("TreasureCoinGold", coords);
                _hands.TryPickupAnyHand(eater, coin);
                break;
            }
            case "SlimeOil":
                AddEffect(eater, "tar", TimeSpan.FromSeconds(10));
                break;
            case "SlimeBlack":
                AddEffect(eater, "spook", TimeSpan.FromSeconds(30));
                break;
            case "SlimeLightPink":
                AddEffect(eater, "peace", TimeSpan.FromSeconds(10));
                break;
            case "SlimeAdamantine":
                AddEffect(eater, "adamantinecookie", TimeSpan.FromSeconds(10));
                break;
        }
    }

    [Dependency] private readonly Content.Shared.Medical.VomitSystem _vomit = default!;

    #endregion
}

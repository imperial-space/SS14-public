using System.Linq;
using System.Numerics;
using Content.Server.Anomaly;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Botany.Components;
using Content.Server.Botany.Systems;
using Content.Server.Chat.Systems;
using Content.Server.Imperial.Uplink.Stealth;
using Content.Server.Polymorph.Systems;
using Content.Server.Popups;
using Content.Server.Research.Systems;
using Content.Shared.Actions;
using Content.Shared.Anomaly.Components;
using Content.Shared.Atmos;
using Content.Shared.Body;
using Content.Shared.Buckle.Components;
using Content.Shared.Chat;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Clothing;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Research.Items;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Materials;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Polymorph;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Projectiles;
using Content.Shared.Research.Components;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Content.Shared.Wieldable.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Research.Items;

/// <summary>
/// Предметы сервиса, ботаники и науки из исследований TG.
/// </summary>
public sealed class ImperialTgServiceItemsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly BotanySystem _botany = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly UplinkStealthSystem _stealth = default!;
    [Dependency] private readonly Content.Server.Materials.MaterialStorageSystem _materials = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly AnomalySystem _anomaly = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedVisualBodySystem _visualBody = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly Content.Shared.Storage.EntitySystems.SharedStorageSystem _storage = default!;
    [Dependency] private readonly ResearchSystem _research = default!;
    [Dependency] private readonly SharedEmpSystem _emp = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ImperialPortaseederComponent, GetVerbsEvent<AlternativeVerb>>(OnPortaseederVerbs);
        SubscribeLocalEvent<ImperialPortaseederComponent, ExaminedEvent>((_, _, args) => args.PushMarkup(Loc.GetString("imperial-portaseeder-examine")));
        SubscribeLocalEvent<ImperialFloraBeamComponent, ProjectileHitEvent>(OnFloraHit);
        SubscribeLocalEvent<ImperialGeneShearsComponent, AfterInteractEvent>(OnShearsInteract);
        SubscribeLocalEvent<ImperialGeneShearsComponent, ImperialGeneShearsDoAfterEvent>(OnShearsDoAfter);

        SubscribeLocalEvent<ImperialHeatingMugComponent, UseInHandEvent>(OnMugUse);

        SubscribeLocalEvent<ImperialHypnochairComponent, GetVerbsEvent<AlternativeVerb>>(OnHypnochairVerbs);
        SubscribeLocalEvent<ImperialHypnochairComponent, ImperialHypnochairDoAfterEvent>(OnHypnochairDoAfter);


        SubscribeLocalEvent<ImperialDeepFryerComponent, GetVerbsEvent<AlternativeVerb>>(OnFryerVerbs);
        SubscribeLocalEvent<ImperialDeepFryerComponent, ImperialDeepFryDoAfterEvent>(OnFryDoAfter);

        SubscribeLocalEvent<ImperialGibberComponent, DragDropTargetEvent>(OnGibberDrop);
        SubscribeLocalEvent<ImperialGibberComponent, ImperialGibberDoAfterEvent>(OnGibberDoAfter);

        SubscribeLocalEvent<ImperialSpaceFurnaceComponent, UseInHandEvent>(OnFurnaceUse);

        SubscribeLocalEvent<ImperialAnomalyNeutralizerComponent, AfterInteractEvent>(OnNeutralizerInteract);

        SubscribeLocalEvent<ImperialPolymorphBeltComponent, AfterInteractEvent>(OnBeltInteract);
        SubscribeLocalEvent<ImperialPolymorphBeltComponent, GetVerbsEvent<ActivationVerb>>(OnBeltVerbs);

        SubscribeLocalEvent<ImperialPerceptomatrixComponent, ClothingGotEquippedEvent>(OnHelmEquipped);
        SubscribeLocalEvent<ImperialPerceptomatrixComponent, ClothingGotUnequippedEvent>(OnHelmUnequipped);
        SubscribeLocalEvent<ImperialPerceptomatrixComponent, ImperialPerceptomatrixActionEvent>(OnHelmAction);

        SubscribeLocalEvent<ImperialTapeRecorderComponent, GetVerbsEvent<AlternativeVerb>>(OnRecorderVerbs);
        SubscribeLocalEvent<ImperialTapeRecorderComponent, ListenEvent>(OnRecorderListen);

        SubscribeLocalEvent<ImperialRazorComponent, AfterInteractEvent>(OnRazorInteract);
        SubscribeLocalEvent<ImperialRazorComponent, ImperialRazorDoAfterEvent>(OnRazorDoAfter);

        SubscribeLocalEvent<ImperialPaintRemoverComponent, AfterInteractEvent>(OnPaintRemover);

        SubscribeLocalEvent<ImperialExperimentorComponent, GetVerbsEvent<Verb>>(OnExperimentorVerbs);

        SubscribeLocalEvent<ImperialCrystallizerComponent, GetVerbsEvent<AlternativeVerb>>(OnCrystallizerVerbs);
        SubscribeLocalEvent<ImperialCrystallizerComponent, ExaminedEvent>(OnCrystallizerExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var mugs = EntityQueryEnumerator<ImperialHeatingMugComponent>();
        while (mugs.MoveNext(out var uid, out var mug))
        {
            if (!mug.Enabled || !_solution.TryGetSolution(uid, mug.Solution, out var soln, out var sol) || sol.Volume <= 0)
                continue;
            if (sol.Temperature < mug.TargetTemperature)
                _solution.SetTemperature(soln.Value, MathF.Min(mug.TargetTemperature, sol.Temperature + mug.HeatPerSecond * frameTime));
        }

        var furnaces = EntityQueryEnumerator<ImperialSpaceFurnaceComponent, TransformComponent>();
        while (furnaces.MoveNext(out var uid, out var furnace, out var xform))
        {
            if (!furnace.Enabled || now < furnace.NextUpdate)
                continue;
            furnace.NextUpdate = now + TimeSpan.FromSeconds(2);
            UpdateFurnace(uid, furnace, xform);
        }

        var recorders = EntityQueryEnumerator<ImperialTapeRecorderComponent>();
        while (recorders.MoveNext(out var uid, out var rec))
        {
            if (rec.PlaybackIndex < 0 || now < rec.NextLine)
                continue;
            if (rec.PlaybackIndex >= rec.Lines.Count)
            {
                rec.PlaybackIndex = -1;
                continue;
            }
            rec.NextLine = now + TimeSpan.FromSeconds(2);
            _chat.TrySendInGameICMessage(uid, rec.Lines[rec.PlaybackIndex++], InGameICChatType.Speak, ChatTransmitRange.Normal, ignoreActionBlocker: true);
        }

        var brooms = EntityQueryEnumerator<ImperialPushBroomComponent, WieldableComponent>();
        while (brooms.MoveNext(out var uid, out var broom, out var wield))
        {
            if (wield.Wielded && _container.TryGetContainingContainer((uid, null, null), out var holder))
                Sweep(holder.Owner, broom);
        }

        var crystallizers = EntityQueryEnumerator<ImperialCrystallizerComponent>();
        while (crystallizers.MoveNext(out var uid, out var cryst))
            UpdateCrystallizer(uid, cryst, frameTime);
    }

    #region Ботаника

    /// <summary>item_ctrl_click(): seedify(plant, 1) — по одному пакету семян на плод, семена остаются в сумке.</summary>
    private void OnPortaseederVerbs(Entity<ImperialPortaseederComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("imperial-portaseeder-verb"),
            Priority = 3,
            Act = () => Seedify(ent, user),
        });
    }

    private void Seedify(Entity<ImperialPortaseederComponent> ent, EntityUid user)
    {
        if (!TryComp<StorageComponent>(ent, out var storage))
            return;

        var made = 0;
        foreach (var item in storage.Container.ContainedEntities.ToList())
        {
            if (!TryComp<ProduceComponent>(item, out var produce) || !_botany.TryGetSeed(produce, out var seed))
                continue;

            QueueDel(item);
            _container.Remove(item, storage.Container);
            var packet = _botany.SpawnSeedPacket(seed, Transform(ent).Coordinates, user);
            _storage.Insert(ent, packet, out _, user, storage, playSound: false);
            made++;
        }

        _popup.PopupEntity(Loc.GetString("imperial-portaseeder-done", ("count", made)), user, user);
    }

    private void OnFloraHit(Entity<ImperialFloraBeamComponent> ent, ref ProjectileHitEvent args)
    {
        if (!TryComp<PlantHolderComponent>(args.Target, out var holder) || holder.Seed == null)
            return;

        switch (ent.Comp.Mode)
        {
            case ImperialFloraMode.Yield:
                if (_random.Prob(0.3f))
                    holder.YieldMod = Math.Min(holder.YieldMod + 1, 5);
                break;
            case ImperialFloraMode.Mutate:
                holder.MutationLevel += 25;
                break;
        }
    }

    private void OnShearsInteract(Entity<ImperialGeneShearsComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !TryComp<PlantHolderComponent>(target, out var holder) || holder.Seed == null)
            return;

        args.Handled = true;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay,
            new ImperialGeneShearsDoAfterEvent(), ent, target: target, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnShearsDoAfter(Entity<ImperialGeneShearsComponent> ent, ref ImperialGeneShearsDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || !TryComp<PlantHolderComponent>(target, out var holder) || holder.Seed == null)
            return;

        args.Handled = true;
        var seed = holder.Seed.Clone();
        var traits = new List<string>();
        if (seed.Ligneous) traits.Add("ligneous");
        if (seed.Seedless) traits.Add("seedless");
        if (seed.TurnIntoKudzu) traits.Add("kudzu");
        if (seed.CanScream) traits.Add("scream");

        if (traits.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("imperial-shears-nothing"), target, args.User);
            return;
        }

        var cut = _random.Pick(traits);
        switch (cut)
        {
            case "ligneous": seed.Ligneous = false; break;
            case "seedless": seed.Seedless = false; break;
            case "kudzu": seed.TurnIntoKudzu = false; break;
            case "scream": seed.CanScream = false; break;
        }

        holder.Seed = seed;
        holder.Health = Math.Max(1, holder.Health - 15);
        _popup.PopupEntity(Loc.GetString("imperial-shears-done", ("trait", Loc.GetString($"imperial-shears-trait-{cut}"))), target, args.User);
    }

    #endregion

    #region Кружка, кресло, фритюрница, мясорубка

    private void OnMugUse(Entity<ImperialHeatingMugComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        ent.Comp.Enabled = !ent.Comp.Enabled;
        _popup.PopupEntity(Loc.GetString(ent.Comp.Enabled ? "imperial-mug-on" : "imperial-mug-off"), ent, args.User);
    }

    private void OnHypnochairVerbs(Entity<ImperialHypnochairComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !TryComp<StrapComponent>(ent, out var strap))
            return;

        var buckledList = strap.BuckledEntities;
        var victim = buckledList.FirstOrDefault(e => HasComp<HumanoidProfileComponent>(e));
        if (victim == default || victim == args.User)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("imperial-hypnochair-verb"),
            Act = () =>
            {
                _popup.PopupEntity(Loc.GetString("imperial-hypnochair-start", ("target", victim)), ent, Robust.Shared.Player.Filter.Pvs(ent), true, Content.Shared.Popups.PopupType.MediumCaution);
                _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.Duration,
                    new ImperialHypnochairDoAfterEvent(), ent, target: victim)
                {
                    BreakOnMove = true,
                    DistanceThreshold = 2f,
                });
            },
        });
    }

    private void OnHypnochairDoAfter(Entity<ImperialHypnochairComponent> ent, ref ImperialHypnochairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } victim)
            return;

        if (!TryComp<StrapComponent>(ent, out var strap))
            return;
        var strapped = strap.BuckledEntities;
        if (!strapped.Contains(victim))
            return;

        args.Handled = true;
        _stealth.StartTrance(victim);
    }

    private void OnFryerVerbs(Entity<ImperialDeepFryerComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var item = _slots.GetItemOrNull(ent, ent.Comp.Slot);
        if (item == null || HasComp<ImperialDeepFriedComponent>(item))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("imperial-fryer-verb"),
            Act = () => _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.FryTime,
                new ImperialDeepFryDoAfterEvent(), ent, target: ent)
            {
                BreakOnMove = false,
                DistanceThreshold = 3f,
            }),
        });
    }

    private void OnFryDoAfter(Entity<ImperialDeepFryerComponent> ent, ref ImperialDeepFryDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !_power.IsPowered(ent.Owner))
            return;

        args.Handled = true;
        var item = _slots.GetItemOrNull(ent, ent.Comp.Slot);
        if (item == null)
            return;

        EnsureComp<ImperialDeepFriedComponent>(item.Value);
        _metaData.SetEntityName(item.Value, Loc.GetString("imperial-fryer-name", ("name", Name(item.Value))));
        _appearance.SetData(item.Value, ImperialFriedVisuals.Fried, true);
        _popup.PopupEntity(Loc.GetString("imperial-fryer-done", ("item", item.Value)), ent);
    }

    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    private void OnGibberDrop(Entity<ImperialGibberComponent> ent, ref DragDropTargetEvent args)
    {
        if (!TryComp<MobStateComponent>(args.Dragged, out var state) || state.CurrentState == MobState.Alive)
        {
            _popup.PopupEntity(Loc.GetString("imperial-gibber-alive"), ent, args.User);
            return;
        }

        args.Handled = true;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.GibTime,
            new ImperialGibberDoAfterEvent(), ent, target: args.Dragged)
        {
            BreakOnMove = true,
            DistanceThreshold = 2f,
        });
    }

    private void OnGibberDoAfter(Entity<ImperialGibberComponent> ent, ref ImperialGibberDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } body || !_power.IsPowered(ent.Owner))
            return;

        args.Handled = true;
        var coords = _transform.GetMapCoordinates(ent);

        // Снаряжение не перемалывается — выпадает рядом.
        if (_inventory.TryGetContainerSlotEnumerator(body, out var slots))
        {
            while (slots.MoveNext(out var slot))
            {
                if (slot.ContainedEntity is { } worn)
                    _transform.DropNextTo(worn, ent.Owner);
            }
        }
        foreach (var held in _hands.EnumerateHeld(body).ToList())
            _transform.DropNextTo(held, ent.Owner);

        var spawned = 0;
        if (TryComp<ButcherableComponent>(body, out var butcher))
        {
            foreach (var spawn in Content.Shared.Storage.EntitySpawnCollection.GetSpawns(butcher.SpawnedEntities, _random))
            {
                Spawn(spawn, coords);
                spawned++;
            }
        }

        if (spawned == 0)
        {
            for (var i = 0; i < 3; i++)
                Spawn(ent.Comp.DefaultMeat, coords);
        }

        QueueDel(body);
        _popup.PopupEntity(Loc.GetString("imperial-gibber-done"), ent);
    }

    #endregion

    #region Космическая печь

    private void OnFurnaceUse(Entity<ImperialSpaceFurnaceComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        ent.Comp.Enabled = !ent.Comp.Enabled;
        _popup.PopupEntity(Loc.GetString(ent.Comp.Enabled ? "imperial-furnace-on" : "imperial-furnace-off"), ent, args.User);
    }

    private void UpdateFurnace(EntityUid uid, ImperialSpaceFurnaceComponent furnace, TransformComponent xform)
    {
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var center = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        for (var x = -furnace.Radius; x <= furnace.Radius; x++)
        for (var y = -furnace.Radius; y <= furnace.Radius; y++)
        {
            var tile = center + new Vector2i(x, y);
            var mix = _atmos.GetTileMixture(gridUid, null, tile, true);
            if (mix == null || mix.Pressure >= Atmospherics.OneAtmosphere * 0.9f)
                continue;

            // Добавляем стандартный воздух до 1 атм при 20 °C.
            var air = new GasMixture(mix.Volume) { Temperature = Atmospherics.T20C };
            var moles = (Atmospherics.OneAtmosphere - mix.Pressure) * mix.Volume / (Atmospherics.R * Atmospherics.T20C);
            air.AdjustMoles(Gas.Oxygen, moles * Atmospherics.OxygenStandard);
            air.AdjustMoles(Gas.Nitrogen, moles * Atmospherics.NitrogenStandard);
            _atmos.Merge(mix, air);
        }
    }

    #endregion

    #region Нейтрализатор, пояс, шлем

    private void OnNeutralizerInteract(Entity<ImperialAnomalyNeutralizerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !TryComp<AnomalyComponent>(target, out var anomaly))
            return;

        args.Handled = true;
        _anomaly.EndAnomaly(target, anomaly);
        _popup.PopupEntity(Loc.GetString("imperial-neutralizer-done"), target, args.User);
        QueueDel(ent);
    }

    private void OnBeltInteract(Entity<ImperialPolymorphBeltComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || target == args.User || !HasComp<MobStateComponent>(target))
            return;

        args.Handled = true;
        if (HasComp<HumanoidProfileComponent>(target) || MetaData(target).EntityPrototype is not { } proto)
        {
            _popup.PopupEntity(Loc.GetString("imperial-polybelt-cant"), args.User, args.User);
            return;
        }

        ent.Comp.Stored = proto.ID;
        _popup.PopupEntity(Loc.GetString("imperial-polybelt-scanned", ("target", target)), args.User, args.User);
    }

    private void OnBeltVerbs(Entity<ImperialPolymorphBeltComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Stored is not { } stored)
            return;

        var user = args.User;
        args.Verbs.Add(new ActivationVerb
        {
            Text = Loc.GetString("imperial-polybelt-verb"),
            Act = () =>
            {
                _polymorph.PolymorphEntity(user, new PolymorphConfiguration
                {
                    Entity = stored,
                    Duration = (int) ent.Comp.Duration,
                    Forced = false,
                    Inventory = PolymorphInventoryChange.Drop,
                    RevertOnDeath = true,
                    RevertOnCrit = true,
                });
            },
        });
    }

    private void OnHelmEquipped(Entity<ImperialPerceptomatrixComponent> ent, ref ClothingGotEquippedEvent args)
    {
        _actions.AddAction(args.Wearer, ref ent.Comp.ActionEntity, ent.Comp.Action, ent);
    }

    private void OnHelmUnequipped(Entity<ImperialPerceptomatrixComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        _actions.RemoveAction(args.Wearer, ent.Comp.ActionEntity);
    }

    private void OnHelmAction(Entity<ImperialPerceptomatrixComponent> ent, ref ImperialPerceptomatrixActionEvent args)
    {
        if (args.Handled || !HasComp<MobStateComponent>(args.Target))
            return;

        args.Handled = true;
        _status.TryAddStatusEffectDuration(args.Target, "StatusEffectSeeingRainbow", TimeSpan.FromSeconds(ent.Comp.Duration));
        _popup.PopupEntity(Loc.GetString("imperial-perceptomatrix-hit"), args.Target, args.Target, Content.Shared.Popups.PopupType.MediumCaution);
    }

    #endregion

    #region Магнитофон, бритва, краска, метла

    private void OnRecorderVerbs(Entity<ImperialTapeRecorderComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.Recording ? "imperial-recorder-stop" : "imperial-recorder-record"),
            Priority = 2,
            Act = () =>
            {
                ent.Comp.Recording = !ent.Comp.Recording;
                ent.Comp.PlaybackIndex = -1;
                if (ent.Comp.Recording)
                {
                    ent.Comp.Lines.Clear();
                    EnsureComp<ActiveListenerComponent>(ent).Range = 4f;
                }
                else
                    RemComp<ActiveListenerComponent>(ent);
                _popup.PopupEntity(Loc.GetString(ent.Comp.Recording ? "imperial-recorder-recording" : "imperial-recorder-stopped"), ent, user);
            },
        });
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("imperial-recorder-play"),
            Priority = 1,
            Act = () =>
            {
                ent.Comp.Recording = false;
                RemComp<ActiveListenerComponent>(ent);
                ent.Comp.PlaybackIndex = 0;
                ent.Comp.NextLine = _timing.CurTime;
            },
        });
    }

    private void OnRecorderListen(Entity<ImperialTapeRecorderComponent> ent, ref ListenEvent args)
    {
        if (!ent.Comp.Recording || args.Source == ent.Owner || ent.Comp.Lines.Count >= ent.Comp.MaxLines)
            return;

        ent.Comp.Lines.Add($"{Name(args.Source)}: {args.Message}");
    }

    private void OnRazorInteract(Entity<ImperialRazorComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<HumanoidProfileComponent>(target))
            return;

        args.Handled = true;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay,
            new ImperialRazorDoAfterEvent(), ent, target: target, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnRazorDoAfter(Entity<ImperialRazorComponent> ent, ref ImperialRazorDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;
        if (!_visualBody.TryGatherMarkingsData(target, null, out _, out _, out var applied))
            return;

        // Сначала борода, если её нет — волосы.
        var layer = applied.Values.Any(l => l.TryGetValue(HumanoidVisualLayers.FacialHair, out var m) && m.Count > 0)
            ? HumanoidVisualLayers.FacialHair
            : HumanoidVisualLayers.Hair;

        var changed = false;
        foreach (var layers in applied.Values)
        {
            if (layers.TryGetValue(layer, out var list) && list.Count > 0)
            {
                list.Clear();
                changed = true;
            }
        }

        if (!changed)
        {
            _popup.PopupEntity(Loc.GetString("imperial-razor-nothing"), target, args.User);
            return;
        }

        _visualBody.ApplyMarkings(target, applied);
        _popup.PopupEntity(Loc.GetString(layer == HumanoidVisualLayers.FacialHair ? "imperial-razor-beard" : "imperial-razor-hair", ("target", target)), target, args.User);
    }

    private void OnPaintRemover(Entity<ImperialPaintRemoverComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (RemComp<Content.Shared.SprayPainter.Components.PaintedComponent>(target))
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("imperial-paint-removed", ("target", target)), target, args.User);
        }
    }

    private void Sweep(EntityUid user, ImperialPushBroomComponent broom)
    {
        var xform = Transform(user);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var dir = xform.LocalRotation.GetCardinalDir().ToIntVec();
        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var front = tile + dir;
        var moved = 0;
        foreach (var item in _lookup.GetLocalEntitiesIntersecting(gridUid, front, flags: LookupFlags.Dynamic | LookupFlags.Sundries).ToList())
        {
            if (moved >= broom.MaxItems || !HasComp<ItemComponent>(item) || Transform(item).Anchored || _container.IsEntityInContainer(item))
                continue;

            _transform.SetCoordinates(item, _map.GridTileToLocal(gridUid, grid, front + dir));
            moved++;
        }
    }

    #endregion

    #region E.X.P.E.R.I-MENTOR

    private void OnExperimentorVerbs(Entity<ImperialExperimentorComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || _slots.GetItemOrNull(ent, ent.Comp.Slot) is not { } item)
            return;

        var user = args.User;
        foreach (var type in Enum.GetValues<ImperialExperimentType>())
        {
            var t = type;
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString($"imperial-experimentor-{t.ToString().ToLowerInvariant()}"),
                Category = VerbCategory.SelectType,
                Act = () => Experiment(ent, user, item, t),
            });
        }
    }

    private void Experiment(Entity<ImperialExperimentorComponent> ent, EntityUid user, EntityUid item, ImperialExperimentType type)
    {
        if (_timing.CurTime < ent.Comp.NextUse || !_power.IsPowered(ent.Owner))
            return;

        ent.Comp.NextUse = _timing.CurTime + ent.Comp.Cooldown;
        var coords = _transform.GetMapCoordinates(ent);
        var roll = _random.NextFloat();

        // Очки исследований за опыт, как discover/«критическая реакция» в experimentor.dm.
        var server = FindServer(ent);
        if (server != null && roll < 0.3f)
            _research.ModifyServerPoints(server.Value, 500);

        switch (type)
        {
            case ImperialExperimentType.Poke:
                if (roll < 0.1f)
                    _transform.DropNextTo(item, ent.Owner);
                break;
            case ImperialExperimentType.Irradiate:
                if (roll < 0.15f)
                    _emp.EmpPulse(coords, 2f, 5000f, TimeSpan.FromSeconds(5));
                break;
            case ImperialExperimentType.Gas:
                if (roll < 0.2f)
                    Spawn("Smoke", coords);
                break;
            case ImperialExperimentType.Heat:
            {
                var damage = new DamageSpecifier();
                damage.DamageDict["Heat"] = 15;
                foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 1.5f))
                    if (roll < 0.15f)
                        _damageable.TryChangeDamage(mob.Owner, damage);
                break;
            }
            case ImperialExperimentType.Cold:
                if (roll < 0.2f)
                    Spawn("Smoke", coords);
                break;
            case ImperialExperimentType.Obliterate:
                QueueDel(item);
                if (server != null)
                    _research.ModifyServerPoints(server.Value, 250);
                break;
        }

        _popup.PopupEntity(Loc.GetString($"imperial-experimentor-result-{type.ToString().ToLowerInvariant()}", ("item", item)), ent, user);
    }

    private EntityUid? FindServer(EntityUid ent)
    {
        var query = EntityQueryEnumerator<ResearchServerComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (Transform(uid).MapID == Transform(ent).MapID)
                return uid;
        }
        return null;
    }

    #endregion

    #region Кристаллизатор

    private void OnCrystallizerVerbs(Entity<ImperialCrystallizerComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.Enabled ? "imperial-crystallizer-off" : "imperial-crystallizer-on"),
            Priority = 2,
            Act = () => ent.Comp.Enabled = !ent.Comp.Enabled,
        });

        for (var i = 0; i < ent.Comp.Recipes.Count; i++)
        {
            var index = i;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("imperial-crystallizer-select", ("recipe", Loc.GetString(ent.Comp.Recipes[i].Name))),
                Priority = 0,
                Act = () =>
                {
                    ent.Comp.Selected = index;
                    ent.Comp.Progress = 0;
                    _popup.PopupEntity(Loc.GetString("imperial-crystallizer-selected", ("recipe", Loc.GetString(ent.Comp.Recipes[index].Name))), ent, user);
                },
            });
        }
    }

    private void OnCrystallizerExamined(Entity<ImperialCrystallizerComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Recipes.Count == 0)
            return;

        var recipe = ent.Comp.Recipes[Math.Clamp(ent.Comp.Selected, 0, ent.Comp.Recipes.Count - 1)];
        var gases = string.Join(", ", recipe.Gases.Select(g => $"{Loc.GetString(_atmos.GetGas(g.Key).Name)} {g.Value}"));
        args.PushMarkup(Loc.GetString("imperial-crystallizer-examine",
            ("recipe", Loc.GetString(recipe.Name)), ("gases", gases),
            ("progress", (int) (ent.Comp.Progress / recipe.Time * 100))));
    }

    private void UpdateCrystallizer(EntityUid uid, ImperialCrystallizerComponent comp, float frameTime)
    {
        if (!comp.Enabled || comp.Recipes.Count == 0 || !_power.IsPowered(uid))
            return;

        var recipe = comp.Recipes[Math.Clamp(comp.Selected, 0, comp.Recipes.Count - 1)];
        var mix = _atmos.GetContainingMixture(uid, excite: true);
        if (mix == null)
            return;

        // Работает, только пока на тайле достаточно нужных газов.
        foreach (var (gas, moles) in recipe.Gases)
        {
            if (mix.GetMoles(gas) < moles * frameTime / recipe.Time)
                return;
        }

        foreach (var (gas, moles) in recipe.Gases)
            mix.AdjustMoles(gas, -moles * frameTime / recipe.Time);

        comp.Progress += frameTime;
        if (comp.Progress < recipe.Time)
            return;

        comp.Progress = 0;
        Spawn(recipe.Result, Transform(uid).Coordinates);
    }

    #endregion
}

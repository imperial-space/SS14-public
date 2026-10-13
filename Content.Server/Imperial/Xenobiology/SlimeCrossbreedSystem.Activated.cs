using Content.Shared.Stacks;
using Robust.Shared.Map.Components;
using Content.Shared.Random.Helpers;
using Content.Shared.Prototypes;
using Content.Shared.Coordinates.Helpers;
using Robust.Shared.Random;
using System.Linq;
using Content.Server.Imperial.Xenobiology.Components;
using Content.Server.Power.Components;
using Content.Shared.Atmos;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.Light.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Tag;
using Content.Shared.Destructible;
using Content.Shared.Imperial.Xenobiology;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology;

public sealed partial class SlimeCrossbreedSystem
{
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly SharedPoweredLightSystem _lights = default!;
    [Dependency] private readonly SharedDestructibleSystem _destructible = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> WallTag = "Wall";
    private static readonly ProtoId<TagPrototype> AirlockTag = "Airlock";

    #region Burning

    /// <summary>burning.dm: наступательные эффекты.</summary>
    private bool Burning(EntityUid ext, string colour, EntityUid user)
    {
        var coords = At(user);
        switch (colour)
        {
            case "SlimeGrey":
            {
                var slime = _slime.SpawnSlime(coords, "SlimeGrey");
                var comp = Comp<SlimeComponent>(slime);
                _slime.Befriend((slime, comp), user);
                _slime.AdjustNutrition((slime, comp), SlimeConstants.HungerNutrition - comp.Nutrition);
                _temperature.ForceChangeTemperature(slime, Atmospherics.T0C + 400f);
                Popup("slime-cross-burning-grey", slime, ("user", user));
                break;
            }
            case "SlimeOrange":
                Popup("slime-cross-burning-orange", user, ("extract", ext));
                ChemSmoke(coords, "CapsaicinOil", 100f, 7);
                break;
            case "SlimePurple":
                Popup("slime-cross-burning-purple", user, ("extract", ext));
                Spawn("SlimeAutoinjectorStimulant", coords);
                break;
            case "SlimeBlue":
                Popup("slime-cross-burning-blue", user, ("extract", ext));
                foreach (var tile in TilesAround(coords, 3))
                {
                    _puddle.TrySpillAt(tile, new Content.Shared.Chemistry.Components.Solution("Ice", 5), out _, sound: false);
                }

                foreach (var mob in _lookup.GetEntitiesInRange<HumanoidProfileComponent>(coords, 5f))
                {
                    if (mob.Owner == user)
                        continue;
                    _temperature.ForceChangeTemperature(mob, 260f + 10f);
                    PopupUser("slime-cross-burning-blue-chill", mob);
                }
                break;
            case "SlimeMetal":
                foreach (var wall in _lookup.GetEntitiesInRange(coords, 1.5f, LookupFlags.Static).ToList())
                {
                    if (_tag.HasTag(wall, WallTag))
                        _destructible.DestroyEntity(wall);
                }
                Popup("slime-cross-burning-metal", user, ("extract", ext));
                break;
            case "SlimeYellow":
                Popup("slime-cross-burning-yellow", user, ("extract", ext));
                foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 4f))
                {
                    if (mob.Owner == user)
                        continue;
                    if (!_electrocution.TryDoElectrocution(mob, ext, 25, TimeSpan.FromSeconds(2), true))
                        Damage(mob, "Shock", 25f);
                    PopupUser("slime-cross-burning-yellow-pulse", mob);
                }
                break;
            case "SlimeDarkPurple":
                Popup("slime-cross-burning-darkpurple", user, ("extract", ext));
                SpawnGas(user, Gas.Plasma, 60f);
                break;
            case "SlimeDarkBlue":
                Popup("slime-cross-burning-darkblue", user, ("extract", ext));
                InjectReagent(user, "RegenJelly", 10f);
                ChemSmoke(coords, "FrostOil", 40f, 7);
                break;
            case "SlimeSilver":
            {
                var foods = _extract.Foods();
                var amount = _random.Next(3, 7);
                for (var i = 0; i < amount && foods.Count > 0; i++)
                {
                    var food = Spawn(_random.Pick(foods), Scatter(coords, 1));
                    if (_solutions.TryGetSolution(food, "food", out var sol, out _))
                        _solutions.TryAddReagent(sol.Value, "SlimeJelly", 5);
                }
                Popup("slime-cross-burning-silver", user, ("extract", ext));
                break;
            }
            case "SlimeBluespace":
                Popup("slime-cross-burning-bluespace", user, ("extract", ext));
                foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 1.5f).ToList())
                {
                    if (mob.Owner == user)
                        continue;
                    _items.BlinkMob(mob, 6);
                }
                break;
            case "SlimeSepia":
                Popup("slime-cross-burning-sepia", user, ("extract", ext));
                Spawn("SlimeRewindCamera", coords);
                break;
            case "SlimeCerulean":
                Popup("slime-cross-potion", user, ("extract", ext));
                Spawn("SlimePotionExtractCloner", coords);
                break;
            case "SlimePyrite":
            {
                var broke = false;
                foreach (var light in _lookup.GetEntitiesInRange<PoweredLightComponent>(coords, 10f))
                {
                    broke |= _lights.TryDestroyBulb(light, light.Comp);
                }
                Popup(broke ? "slime-cross-burning-pyrite" : "slime-cross-burning-pyrite-fail", user, ("extract", ext));
                break;
            }
            case "SlimeRed":
                Popup("slime-cross-burning-red", user, ("extract", ext), ("user", user));
                foreach (var slime in _lookup.GetEntitiesInRange<SlimeComponent>(coords, 7f))
                {
                    if (TryComp<SlimeBrainComponent>(slime, out var brain))
                    {
                        foreach (var friend in brain.Friends.ToList())
                        {
                            if (friend != user)
                                _slime.Unfriend(slime, brain, friend);
                        }
                    }

                    _slime.SetEnraged(slime);
                    Popup("slime-reaction-frenzy", slime, ("slime", slime.Owner));
                }
                break;
            case "SlimeGreen":
                QueueDel(ext);
                if (!_hands.TryPickupAnyHand(user, Spawn("SlimeArmBlade", coords)))
                    Popup("slime-cross-burning-green-fail", user, ("extract", ext), ("user", user));
                else
                    Popup("slime-cross-burning-green", user, ("extract", ext), ("user", user));
                Damage(user, "Heat", 30f);
                return false;
            case "SlimePink":
                Popup("slime-cross-burning-pink", user, ("extract", ext));
                Spawn("SlimeCrossBeakerPax", coords);
                break;
            case "SlimeGold":
                Popup("slime-cross-burning-gold", user, ("extract", ext), ("user", user));
                for (var i = 0; i < 3; i++)
                {
                    var mob = SpawnGoldMob(Scatter(coords), true);
                    MakeAlly(mob, user);
                }
                break;
            case "SlimeOil":
            {
                Popup("slime-cross-burning-oil", user, ("extract", ext), ("user", user));
                Comp<SlimeCrossActivatedComponent>(ext).Active = true;
                Schedule(TimeSpan.FromSeconds(5), () =>
                {
                    if (TerminatingOrDeleted(ext))
                        return;
                    var here = At(ext);
                    _audio.PlayPvs(new Robust.Shared.Audio.SoundCollectionSpecifier("Explosion"), here);
                    // SSexplosions.med_mov_atom: ударная волна без разрушений.
                    _explosion.QueueExplosion(_transform.ToMapCoordinates(here), "Default", 30f, 15f, 10f, user, canCreateVacuum: false);
                    QueueDel(ext);
                });
                return false;
            }
            case "SlimeBlack":
                Popup("slime-cross-burning-black", user, ("extract", ext), ("user", user));
                Schedule(TimeSpan.Zero, () => _polymorph.PolymorphEntity(user, "SlimeCrossSlimeForm"));
                break;
            case "SlimeLightPink":
                Popup("slime-cross-burning-lightpink", user, ("extract", ext));
                foreach (var mob in _lookup.GetEntitiesInRange<HumanoidProfileComponent>(coords, 7f))
                {
                    InjectReagent(mob, "Pax", 5f);
                }
                break;
            case "SlimeAdamantine":
                Popup("slime-cross-burning-adamantine", user, ("extract", ext));
                Spawn("SlimeAdamantineShield", coords);
                break;
            case "SlimeRainbow":
                Popup("slime-cross-burning-rainbow", user, ("extract", ext));
                Spawn("SlimeRainbowKnife", coords);
                break;
        }

        return true;
    }

    #endregion

    #region Charged

    /// <summary>charged.dm: усиленные версии обычных реакций.</summary>
    private bool Charged(EntityUid ext, string colour, EntityUid user)
    {
        var coords = At(user);
        switch (colour)
        {
            case "SlimeGrey":
                Spawn("SlimePotionReviver", coords);
                Popup("slime-cross-potion", user, ("extract", ext));
                break;
            case "SlimeOrange":
                foreach (var tile in TilesAround(coords, 5))
                {
                    Hotspot(tile);
                }
                break;
            case "SlimePurple":
                Spawn("SlimeCrossBeakerOmnizine", coords);
                Popup("slime-cross-charged-purple", user, ("extract", ext));
                break;
            case "SlimeBlue":
                Spawn("SlimePotionChargedStabilizer", coords);
                Popup("slime-cross-potion", user, ("extract", ext));
                break;
            case "SlimeMetal":
                SpawnStack("SheetSteel1", coords, 25);
                SpawnStack("SheetPlasteel1", coords, 10);
                Popup("slime-cross-charged-metal", user, ("extract", ext));
                break;
            case "SlimeYellow":
                Spawn("SlimeHyperchargedCell", coords);
                Popup("slime-cross-charged-yellow", user, ("extract", ext));
                break;
            case "SlimeDarkPurple":
                SpawnStack("SheetPlasma1", coords, 10);
                Popup("slime-cross-charged-darkpurple", user, ("extract", ext));
                break;
            case "SlimeDarkBlue":
                Spawn("SlimePotionSpaceproof", coords);
                Popup("slime-cross-potion", user, ("extract", ext));
                break;
            case "SlimeSilver":
            {
                Spawn("FoodCakeSlime", coords);
                var drinks = _extract.Drinks();
                for (var i = 0; i < 10 && drinks.Count > 0; i++)
                {
                    Spawn(_random.Pick(drinks), Scatter(coords, 1));
                }
                Popup("slime-cross-charged-silver", user, ("extract", ext));
                break;
            }
            case "SlimeBluespace":
                SpawnStack("XenoBluespaceCrystal", coords, 10);
                Popup("slime-cross-charged-bluespace", user, ("extract", ext));
                break;
            case "SlimeSepia":
                Spawn("TravelCamera", coords);
                Popup("slime-cross-charged-sepia", user, ("extract", ext));
                break;
            case "SlimeCerulean":
                Spawn("SlimePotionMaxEnhancer", coords);
                Popup("slime-cross-potion", user, ("extract", ext));
                break;
            case "SlimePyrite":
                SpawnStack("MaterialBananium1", coords, 10);
                Popup("slime-cross-charged-pyrite", user, ("extract", ext));
                break;
            case "SlimeRed":
                Spawn("SlimePotionLavaproof", coords);
                Popup("slime-cross-potion", user, ("extract", ext));
                break;
            case "SlimeGreen":
                // Выбор подвида слаймолюдов: в SS14 подвид один.
                if (!HasComp<HumanoidProfileComponent>(user))
                {
                    PopupUser("slime-cross-charged-green-fail", user);
                    return false;
                }
                Popup("slime-cross-charged-green", user, ("extract", ext), ("user", user));
                Schedule(TimeSpan.Zero, () => _polymorph.PolymorphEntity(user, "SlimeMutationJelly"));
                break;
            case "SlimePink":
                Spawn("SlimePotionLove", coords);
                Popup("slime-cross-potion", user, ("extract", ext));
                break;
            case "SlimeGold":
            {
                Popup("slime-cross-charged-gold", user, ("extract", ext));
                Comp<SlimeCrossActivatedComponent>(ext).Active = true;
                var spawner = EnsureComp<SlimeCrossMobSpawnerComponent>(ext);
                spawner.NextSpawn = _timing.CurTime + TimeSpan.FromSeconds(5);
                return false;
            }
            case "SlimeOil":
                Popup("slime-cross-charged-oil", user, ("extract", ext));
                Comp<SlimeCrossActivatedComponent>(ext).Active = true;
                Schedule(TimeSpan.FromSeconds(5), () =>
                {
                    if (TerminatingOrDeleted(ext))
                        return;
                    // explosion(2, 3, 4)
                    _explosion.QueueExplosion(_transform.ToMapCoordinates(At(ext)), "Default", 300f, 6f, 70f, user);
                    QueueDel(ext);
                });
                return false;
            case "SlimeBlack":
            {
                if (!HasComp<HumanoidProfileComponent>(user))
                {
                    PopupUser("slime-cross-incompatible", user);
                    return false;
                }

                var species = new[] { "SlimeMutationHuman", "SlimeMutationLizard", "SlimeMutationMoth", "SlimeMutationFelinid", "SlimeMutationJelly", "SlimeCrossSpeciesDwarf", "SlimeCrossSpeciesArachnid", "SlimeCrossSpeciesDiona", "SlimeCrossSpeciesVox" };
                PopupUser("slime-cross-charged-black", user);
                var pick = _random.Pick(species);
                Schedule(TimeSpan.Zero, () => _polymorph.PolymorphEntity(user, pick));
                break;
            }
            case "SlimeLightPink":
                Spawn("SlimePotionPeace", coords);
                Popup("slime-cross-potion", user, ("extract", ext));
                break;
            case "SlimeAdamantine":
                // Оболочки големов в SS14 нет: адамантиновый каркас — призрачная роль слуги-голема.
                Popup("slime-cross-charged-adamantine", user, ("extract", ext));
                var golem = Spawn("SlimeGolemShellSpawner", coords);
                if (TryComp<Content.Server.Ghost.Roles.Components.GhostRoleComponent>(golem, out var role))
                    role.RoleRules = Loc.GetString("slime-golem-rules", ("creator", Name(user)));
                break;
            case "SlimeRainbow":
                Popup("slime-cross-charged-rainbow", user, ("extract", ext));
                for (var i = 0; i < 3; i++)
                {
                    _slime.SpawnSlime(Scatter(coords), _slime.RandomSlimeType());
                }
                break;
        }

        return true;
    }

    private void UpdateChargedGold(TimeSpan now)
    {
        var query = EntityQueryEnumerator<SlimeCrossMobSpawnerComponent>();
        while (query.MoveNext(out var uid, out var spawner))
        {
            if (now < spawner.NextSpawn)
                continue;
            spawner.NextSpawn = now + TimeSpan.FromSeconds(2);

            var coords = At(uid);
            Popup("slime-cross-charged-gold-spawn", uid, ("extract", uid));
            _audio.PlayPvs(Sparks, coords);
            SpawnGoldMob(coords, true);
            spawner.Spawned++;
            if (spawner.Spawned < spawner.MaxSpawn)
                continue;

            Popup("slime-cross-charged-gold-done", uid, ("extract", uid));
            QueueDel(uid);
        }
    }

    #endregion

    #region Chilling

    /// <summary>chilling.dm: защитные эффекты.</summary>
    private bool Chilling(EntityUid ext, string colour, EntityUid user)
    {
        var coords = At(user);
        switch (colour)
        {
            case "SlimeGrey":
                Popup("slime-cross-chilling-grey", user, ("extract", ext));
                for (var i = 0; i < 3; i++)
                {
                    Spawn("SlimeBarrierCube", coords);
                }
                break;
            case "SlimeOrange":
                Popup("slime-cross-chilling-orange", user, ("extract", ext));
                foreach (var tile in TilesAround(coords, 2, 2))
                {
                    Hotspot(tile);
                }
                break;
            case "SlimePurple":
                Popup("slime-cross-chilling-purple", user, ("extract", ext));
                foreach (var mob in _lookup.GetEntitiesInRange<HumanoidProfileComponent>(coords, 10f))
                {
                    InjectReagent(mob, "RegenJelly", 10f);
                }
                break;
            case "SlimeBlue":
                Popup("slime-cross-chilling-blue", user, ("extract", ext));
                Spawn("ClothingMaskSlimeRebreather", coords);
                break;
            case "SlimeMetal":
                Popup("slime-cross-chilling-metal", user, ("extract", ext), ("user", user));
                foreach (var tile in TilesAround(coords.SnapToGrid(EntityManager), 1, 1))
                {
                    Spawn("SlimeWall", tile);
                }
                break;
            case "SlimeYellow":
            {
                // Батарея ЛКП ближайшего помещения заряжается на 50%.
                var charged = false;
                foreach (var apc in _lookup.GetEntitiesInRange<ApcComponent>(coords, 12f))
                {
                    if (!TryComp<BatteryComponent>(apc, out var battery))
                        continue;
                    _battery.ChangeCharge((apc, battery), battery.MaxCharge / 2f);
                    charged = true;
                    break;
                }

                Popup(charged ? "slime-cross-chilling-yellow" : "slime-cross-chilling-yellow-fail", user, ("extract", ext));
                break;
            }
            case "SlimeDarkPurple":
            {
                var filtered = false;
                foreach (var tile in TilesAround(coords, 7))
                {
                    var mixture = _atmos.GetTileMixture(tile.EntityId, excite: true);
                    if (mixture == null || mixture.GetMoles(Gas.Plasma) <= 0f)
                        continue;
                    mixture.SetMoles(Gas.Plasma, 0f);
                    filtered = true;
                }

                foreach (var tile in TilesAround(coords, 7))
                {
                    if (Transform(tile.EntityId).GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
                        continue;
                    var indices = _map.TileIndicesFor(grid, gridComp, tile);
                    var mixture = _atmos.GetTileMixture(grid, null, indices, true);
                    if (mixture == null || mixture.GetMoles(Gas.Plasma) <= 0f)
                        continue;
                    mixture.SetMoles(Gas.Plasma, 0f);
                    filtered = true;
                }

                Popup(filtered ? "slime-cross-chilling-darkpurple" : "slime-cross-chilling-darkpurple-fail", user, ("extract", ext));
                break;
            }
            case "SlimeDarkBlue":
                Popup("slime-cross-chilling-darkblue", user, ("extract", ext), ("user", user));
                FreezeInIce(user, true);
                break;
            case "SlimeSilver":
            {
                Popup("slime-cross-chilling-silver", user, ("extract", ext));
                var amount = _random.Next(5, 11);
                for (var i = 0; i < amount; i++)
                {
                    Spawn("FoodTinMRE", Scatter(coords, 1));
                }
                break;
            }
            case "SlimeBluespace":
            {
                var link = Comp<SlimeCrossLinkComponent>(ext);
                if (link.Linked.Count == 0)
                {
                    PopupUser("slime-cross-not-linked", user, ("extract", ext));
                    return false;
                }

                PopupUser("slime-cross-chilling-bluespace-start", user, ("extract", ext));
                var activated = Comp<SlimeCrossActivatedComponent>(ext);
                activated.Active = true;
                foreach (var mob in link.Linked)
                {
                    if (!TerminatingOrDeleted(mob))
                        PopupUser("slime-cross-recall-tug", mob);
                }

                _doAfter.TryStartDoAfter(new Content.Shared.DoAfter.DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(10),
                    new SlimeCrossRecallDoAfterEvent(), ext, used: ext)
                {
                    BreakOnMove = true,
                    BreakOnDamage = true,
                });
                return false;
            }
            case "SlimeSepia":
            {
                var link = Comp<SlimeCrossLinkComponent>(ext);
                Popup("slime-cross-chilling-sepia", user, ("extract", ext));
                var stop = Spawn("SlimeTimestop", coords);
                var stopComp = Comp<SlimeTimestopComponent>(stop);
                stopComp.Duration = TimeSpan.FromSeconds(30);
                stopComp.End = _timing.CurTime + stopComp.Duration;
                foreach (var pal in link.Linked)
                {
                    if (pal != user)
                        stopComp.Immune.Add(pal);
                }
                break;
            }
            case "SlimeCerulean":
                Popup("slime-cross-chilling-cerulean", user, ("extract", ext), ("user", user));
                MakeSlimeClone(user);
                break;
            case "SlimePyrite":
                Popup("slime-cross-chilling-pyrite", user, ("extract", ext));
                Spawn("ClothingEyesSlimePrismGlasses", coords);
                break;
            case "SlimeRed":
            {
                var found = false;
                foreach (var slime in _lookup.GetEntitiesInRange<SlimeComponent>(coords, 7f))
                {
                    found = true;
                    _slime.SetPacified(slime);
                }
                Popup(found ? "slime-cross-chilling-red" : "slime-cross-chilling-red-fail", user, ("extract", ext));
                break;
            }
            case "SlimeGreen":
                QueueDel(ext);
                if (!_hands.TryPickupAnyHand(user, Spawn("SlimeBloodChiller", coords)))
                    Popup("slime-cross-chilling-green-fail", user, ("extract", ext), ("user", user));
                else
                    Popup("slime-cross-chilling-green", user, ("extract", ext), ("user", user));
                Damage(user, "Heat", 30f);
                return false;
            case "SlimePink":
                Popup("slime-cross-chilling-pink", user, ("extract", ext));
                Spawn("MobSlimeCorgiPuppy", coords);
                break;
            case "SlimeGold":
                Popup("slime-cross-chilling-gold", user, ("extract", ext));
                Spawn("SlimeCaptureDevice", coords);
                break;
            case "SlimeOil":
                Popup("slime-cross-chilling-oil", user, ("extract", ext));
                Comp<SlimeCrossActivatedComponent>(ext).Active = true;
                Schedule(TimeSpan.FromSeconds(5), () =>
                {
                    if (TerminatingOrDeleted(ext))
                        return;
                    // explosion(-1, -1, 10): широкий, но лёгкий взрыв.
                    _explosion.QueueExplosion(_transform.ToMapCoordinates(At(ext)), "Default", 400f, 1.2f, 4f, user, canCreateVacuum: false);
                    QueueDel(ext);
                });
                return false;
            case "SlimeBlack":
                if (HasComp<HumanoidProfileComponent>(user))
                {
                    Popup("slime-cross-chilling-black", user, ("extract", ext), ("user", user));
                    AddEffect(user, "golem", null);
                }
                break;
            case "SlimeLightPink":
                Popup("slime-cross-chilling-lightpink", user, ("extract", ext));
                Spawn("ClothingHeadSlimePeaceFlower", coords);
                break;
            case "SlimeAdamantine":
                Popup("slime-cross-chilling-adamantine", user, ("extract", ext));
                Spawn("ClothingOuterArmorSlimeAdamantine", coords);
                break;
            case "SlimeRainbow":
                Popup("slime-cross-chilling-rainbow", user, ("extract", ext));
                foreach (var door in _lookup.GetEntitiesInRange<Content.Shared.Doors.Components.AirlockComponent>(coords, 10f))
                {
                    Spawn("SlimeWallRainbow", At(door));
                }
                break;
        }

        return true;
    }

    /// <summary>Конец do_after синего охлаждающего экстракта: связанные переносятся к владельцу.</summary>
    private void OnRecallDoAfter(Entity<SlimeCrossLinkComponent> ent, ref SlimeCrossRecallDoAfterEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        foreach (var mob in ent.Comp.Linked)
        {
            if (TerminatingOrDeleted(mob))
                continue;

            if (args.Cancelled)
            {
                PopupUser("slime-cross-recall-faded", mob);
                continue;
            }

            Popup("slime-cross-recall-vanish", mob, ("target", mob));
            _transform.SetCoordinates(mob, At(args.User));
            _transform.AttachToGridOrMap(mob);
            PopupUser("slime-cross-recall-arrive", mob, ("user", args.User));
        }

        PopupUser(args.Cancelled ? "slime-cross-chilling-bluespace-fail" : "slime-cross-chilling-bluespace-done", args.User, ("extract", ent.Owner));
        QueueDel(ent);
    }

    #endregion
}

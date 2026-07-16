using System.Collections.Generic;
using System.Linq;
using Content.Shared.Examine;
using Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Lavaland.Anvil;

public sealed class LavalandAnvilSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly Dictionary<string, string> UpgradeMap = new()
    {
        { "WeaponPlasmaCutterLavaland", "WeaponPlasmaCutterMega" },
        { "WeaponPlasmaCutterFanLavaland", "WeaponMiningShotgunMega" },
        { "WeaponMiningAccelerator", "WeaponKineticAcceleratorMega" },
        { "WeaponKineticCrusher", "WeaponMagmiteCrusher" },
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandAnvilComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<LavalandAnvilComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<LavalandAnvilComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Anvil charge recharge
        var anvilQuery = EntityQueryEnumerator<LavalandAnvilComponent>();
        while (anvilQuery.MoveNext(out _, out var comp))
        {
            if (comp.CurrentCharges >= comp.MaxCharges)
                continue;

            comp.RechargeTimer += frameTime;
            if (comp.RechargeTimer >= comp.RechargeTime)
            {
                comp.RechargeTimer -= comp.RechargeTime;
                comp.CurrentCharges++;
            }
        }

        // Temporary upgrade expiry
        var upgradeQuery = EntityQueryEnumerator<TemporaryUpgradeComponent>();
        while (upgradeQuery.MoveNext(out var uid, out var upgrade))
        {
            upgrade.TimeRemaining -= frameTime;
            if (upgrade.TimeRemaining > 0f)
                continue;

            var saved = new List<EntityUid>();
            TransferUpgradesOut(uid, saved);

            var mapCoords = _transform.GetMapCoordinates(uid);
            _popup.PopupEntity(
                "Улучшение истекло! Оружие вернулось в исходное состояние.",
                uid,
                Filter.Pvs(uid, entityManager: EntityManager),
                true,
                PopupType.LargeCaution);

            var reverted = Spawn(upgrade.RevertProto, mapCoords);
            TransferUpgradesIn(reverted, saved);
            QueueDel(uid);
        }
    }

    private void OnExamined(Entity<LavalandAnvilComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup($"Заряды улучшения: [bold]{ent.Comp.CurrentCharges}/{ent.Comp.MaxCharges}[/bold].");
        if (ent.Comp.CurrentCharges < ent.Comp.MaxCharges)
        {
            var remaining = ent.Comp.RechargeTime - ent.Comp.RechargeTimer;
            var minutes = (int) (remaining / 60);
            var seconds = (int) (remaining % 60);
            args.PushMarkup($"Следующий заряд через: [bold]{minutes}:{seconds:D2}[/bold].");
        }
    }

    private void OnInteractUsing(Entity<LavalandAnvilComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var container = _container.EnsureContainer<Container>(ent, ent.Comp.ContainerId);
        var protoId = MetaData(args.Used).EntityPrototype?.ID;

        if (protoId == null)
            return;

        var weaponCount = 0;
        var gibCount = 0;
        var magCount = 0;
        foreach (var item in container.ContainedEntities)
        {
            var id = MetaData(item).EntityPrototype?.ID;
            if (id == "OreGibtonite") gibCount++;
            else if (id == "OreMagmite") magCount++;
            else if (id != null && UpgradeMap.ContainsKey(id)) weaponCount++;
        }

        if (protoId == "OreGibtonite")
        {
            if (gibCount >= 2)
            {
                _popup.PopupEntity("Уже достаточно гибтонита!", ent, args.User);
                return;
            }
        }
        else if (protoId == "OreMagmite")
        {
            if (magCount >= 2)
            {
                _popup.PopupEntity("Уже достаточно магмита!", ent, args.User);
                return;
            }
        }
        else if (UpgradeMap.ContainsKey(protoId))
        {
            if (weaponCount >= 1)
            {
                _popup.PopupEntity("Оружие уже вложено в наковальню!", ent, args.User);
                return;
            }
        }
        else
        {
            _popup.PopupEntity("Этот предмет нельзя улучшить на наковальне.", ent, args.User);
            return;
        }

        _container.Insert(args.Used, container);
        _popup.PopupEntity("Предмет вложен в наковальню.", ent, args.User);
        args.Handled = true;
    }

    private void OnInteractHand(Entity<LavalandAnvilComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        var comp = ent.Comp;
        var container = _container.EnsureContainer<Container>(ent, comp.ContainerId);

        if (container.ContainedEntities.Count == 0)
        {
            _popup.PopupEntity($"Наковальня пуста. Заряды: {comp.CurrentCharges}/{comp.MaxCharges}.", ent, args.User);
            return;
        }

        if (comp.CurrentCharges <= 0)
        {
            var remaining = comp.RechargeTime - comp.RechargeTimer;
            var minutes = (int) (remaining / 60);
            var seconds = (int) (remaining % 60);
            _popup.PopupEntity($"Наковальня перезаряжается... ({minutes}:{seconds:D2})", ent, args.User);
            return;
        }

        string? weaponId = null;
        var weaponEntity = EntityUid.Invalid;
        var gibCount = 0;
        var magCount = 0;

        foreach (var item in container.ContainedEntities)
        {
            var id = MetaData(item).EntityPrototype?.ID;
            if (id == "OreGibtonite") gibCount++;
            else if (id == "OreMagmite") magCount++;
            else if (id != null && UpgradeMap.ContainsKey(id))
            {
                weaponId = id;
                weaponEntity = item;
            }
        }

        if (weaponId == null || gibCount < 2 || magCount < 2)
        {
            _popup.PopupEntity("Нужно: оружие + 2 гибтонита + 2 магмита.", ent, args.User);
            _container.EmptyContainer(container);
            return;
        }

        // Extract slot upgrades from original weapon before it's deleted
        var saved = new List<EntityUid>();
        TransferUpgradesOut(weaponEntity, saved);

        var upgradeId = UpgradeMap[weaponId];
        var items = container.ContainedEntities.ToArray();
        foreach (var item in items)
        {
            _container.Remove(item, container, reparent: false, force: true);
            QueueDel(item);
        }

        var upgraded = Spawn(upgradeId, Transform(ent).Coordinates);
        TransferUpgradesIn(upgraded, saved);

        var tempComp = EnsureComp<TemporaryUpgradeComponent>(upgraded);
        tempComp.RevertProto = weaponId;
        tempComp.TimeRemaining = tempComp.Duration;

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/smash.ogg"), ent);

        comp.CurrentCharges--;
        _popup.PopupEntity(
            $"КЛИНК! Наковальня улучшает оружие! Осталось зарядов: {comp.CurrentCharges}/{comp.MaxCharges}. Действует 20 минут.",
            ent, args.User, PopupType.Large);
        args.Handled = true;
    }

    private void TransferUpgradesOut(EntityUid weapon, List<EntityUid> saved)
    {
        if (TryComp<MiningUpgradeableGunComponent>(weapon, out var gunComp) &&
            _container.TryGetContainer(weapon, gunComp.UpgradesContainerId, out var gunCont))
        {
            foreach (var item in gunCont.ContainedEntities.ToArray())
            {
                _container.Remove(item, gunCont, reparent: false, force: true);
                saved.Add(item);
            }
        }

        if (TryComp<KineticCrusherUpgradeableComponent>(weapon, out var crusherComp) &&
            _container.TryGetContainer(weapon, crusherComp.UpgradesContainerId, out var crusherCont))
        {
            foreach (var item in crusherCont.ContainedEntities.ToArray())
            {
                _container.Remove(item, crusherCont, reparent: false, force: true);
                saved.Add(item);
            }
        }
    }

    private void TransferUpgradesIn(EntityUid weapon, List<EntityUid> upgrades)
    {
        if (upgrades.Count == 0)
            return;

        string? containerId = null;
        if (TryComp<MiningUpgradeableGunComponent>(weapon, out var gunComp))
            containerId = gunComp.UpgradesContainerId;
        else if (TryComp<KineticCrusherUpgradeableComponent>(weapon, out var crusherComp))
            containerId = crusherComp.UpgradesContainerId;

        if (containerId == null)
            return;

        var cont = _container.EnsureContainer<Container>(weapon, containerId);
        foreach (var item in upgrades)
            _container.Insert(item, cont);
    }
}

using System.Linq;
using Content.Server.Antag;
using Content.Server.Body.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Polymorph.Systems;
using Content.Server.Popups;
using Content.Shared.Roles.Components;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Gibbing;
using Content.Shared.Imperial.Lavaland.WheelOfFate;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Lavaland.WheelOfFate;

public sealed class D20FateDiceSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly GibbingSystem _gibbing = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<D20FateDiceComponent, UseInHandEvent>(OnUse);
        SubscribeLocalEvent<FateDamageResistComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
    }

    private void OnBeforeDamage(Entity<FateDamageResistComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Damage != null)
            args.Damage = args.Damage * 0.5f;
    }

    private void OnUse(Entity<D20FateDiceComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;

        var user = args.User;
        var roll = _random.Next(1, 21);

        _audio.PlayPvs(new Robust.Shared.Audio.SoundPathSpecifier("/Audio/Items/Dice/dice1.ogg"), user);
        _popup.PopupEntity(Loc.GetString("d20-fate-dice-roll", ("roll", roll)), user, user, PopupType.Large);

        ApplyEffect(roll, user);
        QueueDel(ent);
    }

    private void ApplyEffect(int roll, EntityUid user)
    {
        var coords = Transform(user).Coordinates;

        switch (roll)
        {
            case 1: // Полное уничтожение
                _popup.PopupCoordinates(Loc.GetString("d20-fate-1"), coords, PopupType.LargeCaution);
                _gibbing.Gib(user, dropGiblets: true);
                break;

            case 2: // Смерть
                _popup.PopupCoordinates(Loc.GetString("d20-fate-2"), coords, PopupType.LargeCaution);
                var deathDmg = new DamageSpecifier();
                deathDmg.DamageDict["Blunt"] = 200;
                deathDmg.DamageDict["Burn"] = 200;
                _damageable.TryChangeDamage(user, deathDmg, ignoreResistances: true);
                break;

            case 3: // Призыв стаи агрессивных созданий
                _popup.PopupCoordinates(Loc.GetString("d20-fate-3"), coords, PopupType.LargeCaution);
                for (var i = 0; i < 5; i++)
                {
                    var offset = new System.Numerics.Vector2(
                        _random.NextFloat(-3f, 3f),
                        _random.NextFloat(-3f, 3f));
                    SpawnAtPosition("MobXeno", coords.Offset(offset));
                }
                break;

            case 4: // Уничтожение всех надетых предметов
                _popup.PopupCoordinates(Loc.GetString("d20-fate-4"), coords, PopupType.LargeCaution);
                DestroyAllItems(user);
                break;

            case 5: // Превращение в макаку
                _popup.PopupCoordinates(Loc.GetString("d20-fate-5"), coords, PopupType.LargeCaution);
                _polymorph.PolymorphEntity(user, "AdminMonkeySmite");
                break;

            case 6: // Постоянное снижение скорости
                _popup.PopupCoordinates(Loc.GetString("d20-fate-6"), coords, PopupType.LargeCaution);
                _speed.ChangeBaseSpeed(user, 1.0f, 1.5f, 20f);
                break;

            case 7: // Оглушение + 50 урона
                _popup.PopupCoordinates(Loc.GetString("d20-fate-7"), coords, PopupType.LargeCaution);
                _stun.TryUpdateParalyzeDuration(user, TimeSpan.FromSeconds(10));
                var stunDmg = new DamageSpecifier();
                stunDmg.DamageDict["Blunt"] = 50;
                _damageable.TryChangeDamage(user, stunDmg, ignoreResistances: true);
                break;

            case 8: // Взрыв
                _popup.PopupCoordinates(Loc.GetString("d20-fate-8"), coords, PopupType.LargeCaution);
                _explosion.QueueExplosion(
                    Transform(user).MapPosition,
                    ExplosionSystem.DefaultExplosionPrototypeId,
                    totalIntensity: 8,
                    slope: 2,
                    maxTileIntensity: 5,
                    cause: user);
                break;

            case 9: // Отсутствие крови
                _popup.PopupCoordinates(Loc.GetString("d20-fate-9"), coords, PopupType.LargeCaution);
                if (TryComp<BloodstreamComponent>(user, out var blood))
                    _bloodstream.TryModifyBloodLevel((user, blood), -9999);
                break;

            case 10: // Ничего
                _popup.PopupCoordinates(Loc.GetString("d20-fate-10"), coords, PopupType.Medium);
                break;

            case 11: // Печенье
                _popup.PopupCoordinates(Loc.GetString("d20-fate-11"), coords, PopupType.Medium);
                SpawnAtPosition("FoodSnackCookieFortune", coords);
                break;

            case 12: // Полное восстановление здоровья
                _popup.PopupCoordinates(Loc.GetString("d20-fate-12"), coords, PopupType.Large);
                HealFull(user);
                break;

            case 13: // Деньги
                _popup.PopupCoordinates(Loc.GetString("d20-fate-13"), coords, PopupType.Large);
                SpawnAtPosition("SpaceCash5000", coords);
                SpawnAtPosition("SpaceCash5000", coords);
                SpawnAtPosition("SpaceCash1000", coords);
                break;

            case 14: // Револьвер
                _popup.PopupCoordinates(Loc.GetString("d20-fate-14"), coords, PopupType.Large);
                SpawnAtPosition("WeaponRevolverMateba", coords);
                break;

            case 15: // Книга заклинаний
                _popup.PopupCoordinates(Loc.GetString("d20-fate-15"), coords, PopupType.Large);
                SpawnAtPosition("SpellbookFireball", coords);
                break;

            case 16: // Сервант
                _popup.PopupCoordinates(Loc.GetString("d20-fate-16"), coords, PopupType.Large);
                SpawnAtPosition("MobCorgi", coords.Offset(new System.Numerics.Vector2(1f, 0f)));
                break;

            case 17: // Припасы синдиката
                _popup.PopupCoordinates(Loc.GetString("d20-fate-17"), coords, PopupType.Large);
                SpawnAtPosition("CrateSyndicateSurplusBundle", coords);
                break;

            case 18: // Карта капитана
                _popup.PopupCoordinates(Loc.GetString("d20-fate-18"), coords, PopupType.Large);
                SpawnAtPosition("CaptainIDCard", coords);
                break;

            case 19: // Уменьшение урона на 50% навсегда
                _popup.PopupCoordinates(Loc.GetString("d20-fate-19"), coords, PopupType.Large);
                EnsureComp<FateDamageResistComponent>(user);
                break;

            case 20: // Становление магом (как через меню антага)
                _popup.PopupCoordinates(Loc.GetString("d20-fate-20"), coords, PopupType.Large);
                if (_playerManager.TryGetSessionByEntity(user, out var session))
                    _antag.ForceMakeAntag<WizardRoleComponent>(session, "Wizard");
                break;
        }
    }

    private void DestroyAllItems(EntityUid user)
    {
        var slots = new[] { "head", "eyes", "ears", "mask", "neck", "jumpsuit", "outerClothing",
            "gloves", "shoes", "id", "belt", "back", "pocket1", "pocket2", "suitstorage" };

        foreach (var slot in slots)
        {
            if (_inventory.TryGetSlotEntity(user, slot, out var item))
            {
                _inventory.TryUnequip(user, slot, true, true);
                if (item.HasValue && Exists(item.Value))
                    QueueDel(item.Value);
            }
        }

        foreach (var held in _hands.EnumerateHeld(user).ToList())
            QueueDel(held);
    }

    private void HealFull(EntityUid user)
    {
        if (!TryComp<DamageableComponent>(user, out var damageable))
            return;

        var currentDamage = _damageable.GetPositiveDamage(new Entity<DamageableComponent>(user, damageable));
        if (currentDamage.Empty) return;

        var heal = new DamageSpecifier();
        foreach (var type in currentDamage.DamageDict.Keys)
            heal.DamageDict[type] = -9999;

        _damageable.TryChangeDamage(user, heal, ignoreResistances: true, interruptsDoAfters: false);
    }
}

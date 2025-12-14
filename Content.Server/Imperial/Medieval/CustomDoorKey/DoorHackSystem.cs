using Content.Server.CustomDoorKey.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Robust.Shared.Audio.Systems;
using Content.Shared.Popups;
using Content.Shared.Doors.Systems;
using Robust.Shared.Random;
using Content.Server.Administration;
using Robust.Shared.Player;
using Content.Server.Prayer;
using Robust.Shared.Audio;
using Content.Server.SpikeTrap.Components;
using Content.Server.MagicBarrier.Components;
using Content.Shared.Doors.Components;
using Content.Server.Doors.Systems;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Server.Imperial.Medieval.GameTicking.Rules;
using Content.Shared.Imperial.Medieval.GameTicking.Rules;

namespace Content.Server.CustomDoorKey
{
    public sealed partial class DoorHackSystem : EntitySystem
    {
        [Dependency] protected readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly SharedDoorSystem _door = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
        [Dependency] private readonly ISharedPlayerManager _sharedPlayerManager = default!;
        [Dependency] private readonly PrayerSystem _prayerSystem = default!;
        [Dependency] private readonly SharedSkillsSystem _skillsSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<DoorHackLockpickComponent, BeforeRangedInteractEvent>(OnUseInHand);
            SubscribeLocalEvent<DoorHackableComponent, ExaminedEvent>(OnExamine);
            SubscribeLocalEvent<DoorHackableComponent, ComponentStartup>(OnStart);
            SubscribeLocalEvent<DoorHackLockpickComponent, ExaminedEvent>(OnExamineKey);
            SubscribeLocalEvent<DoorNewLockComponent, BeforeRangedInteractEvent>(OnUseInHandLock);

        }

        public void OnUseInHandLock(EntityUid uid, DoorNewLockComponent comp, BeforeRangedInteractEvent args)
        {
            if (!args.CanReach)
                return;
            OnUseLock(args.Target, args.User, args.Used, comp);
        }

        public void OnUseLock(EntityUid? target, EntityUid user, EntityUid used, DoorNewLockComponent comp)
        {
            if (target == null)
                return;
            if (TryComp<DoorHackableComponent>(target, out var door) && door != null)
            {
                _audio.PlayPvs(new SoundPathSpecifier(door.EffectSoundOnNewLock), door.Owner);
                door.NumberCount += 2;
                ChangeCode(door.Owner, door);
                QueueDel(used);
                if (!_sharedPlayerManager.TryGetSessionByEntity(user, out var session)) return;
                _prayerSystem.SendSubtleMessage(session, session, "Вы установили новый замок в дверь. Теперь количество зубцов увеличено на 2 и код полностью сброшен.", "Улучшение двери");
            }
        }
        private void OnStart(EntityUid uid, DoorHackableComponent component, ComponentStartup args)
        {
            ChangeCode(uid, component);
        }
        private void ChangeCode(EntityUid uid, DoorHackableComponent component)
        {
            for (int i = 0; i < component.NumberCount; i++)
            {
                component.Numbers[i] = _random.Next(component.MinNumber, component.MaxNumber);
            }
        }
        private void OnExamineKey(EntityUid uid, DoorHackLockpickComponent component, ExaminedEvent args)
        {
            args.PushMarkup("Осталось [color=red]" + component.UseCount + "[/color] использований");
        }

        private void OnExamine(EntityUid uid, DoorHackableComponent component, ExaminedEvent args)
        {
            args.PushMarkup("Прогресс взлома [color=red]" + component.LockPickProgress + "[/color] из [color=green]" + component.NumberCount + "[/color]");
            args.PushMarkup("Длина зубцев в замке от [color=yellow]" + component.MinNumber + "[/color] до [color=yellow]" + component.MaxNumber + "[/color]");
            args.PushMarkup("Количество зубцев в замке [color=red]" + component.NumberCount + "[/color]");
        }

        public void OnUseInHand(EntityUid uid, DoorHackLockpickComponent comp, BeforeRangedInteractEvent args)
        {
            if (!args.CanReach)
                return;
            OnUse(args.Target, args.User, args.Used, comp);
        }

        public void OnUse(EntityUid? target, EntityUid user, EntityUid used, DoorHackLockpickComponent comp)
        {
            if (target == null)
                return;

            if (TryComp<DoorHackableComponent>(target, out var door) && door != null)
            {
                if (HasComp<SkillsComponent>(user) && _skillsSystem.IntelligenceMin(user))
                {
                    _popup.PopupEntity(Loc.GetString("lock-door-popup-low-intelligence"), user, user);
                    return;
                }

                _audio.PlayPvs(new SoundPathSpecifier(comp.EffectSoundOnSucces), door.Owner);
                if (!_sharedPlayerManager.TryGetSessionByEntity(user, out var session)) return;
                _quickDialog.OpenDialog(session, "Взлом", "Число от " + door.MinNumber + " до " + door.MaxNumber, (string message) =>
                {
                    SendHackMessage(message, session, door.MinNumber, door.MaxNumber, door, comp);
                });
            }
        }

        public void SendHackMessage(string message, ICommonSession sender, int min, int max, DoorHackableComponent door, DoorHackLockpickComponent comp)
        {
            if (int.TryParse(message, out int number))
            {
                if (number > max)
                {
                    _prayerSystem.SendSubtleMessage(sender, sender, "Слишком большое значение", "Взлом неверно");
                    return;
                }
                if (number < min)
                {
                    _prayerSystem.SendSubtleMessage(sender, sender, "Слишком маленькое значение", "Взлом неверно");
                    return;
                }

                var lossProb = 1f;
                if (sender.AttachedEntity is { Valid: true } senderEntity)
                {
                    var lossProbEv = new ModifyLockpickLossChanceEvent(1f);
                    RaiseLocalEvent(senderEntity, ref lossProbEv);
                }

                if (_random.Prob(lossProb))
                    comp.UseCount--;

                int rightNumber = door.Numbers[door.LockPickProgress];
                if (number == rightNumber)
                {
                    comp.UseCount++;
                    door.LockPickProgress++;
                    if (door.LockPickProgress < door.NumberCount)
                    {
                        _prayerSystem.SendSubtleMessage(sender, sender, "Число " + number + ". Зубец подобран успешно, осталось еще " + (door.NumberCount - door.LockPickProgress) + " зубцев", "Взлом успех");
                        _audio.PlayPvs(new SoundPathSpecifier(comp.EffectSoundOnNext), door.Owner);
                        return;
                    }
                    else
                    {
                        _prayerSystem.SendSubtleMessage(sender, sender, "Число " + number + ". Дверь успешно взломана", "Взлом успех");
                        _audio.PlayPvs(new SoundPathSpecifier(comp.EffectSoundOnOpen), door.Owner);
                        //_door.TryToggleDoor(door.Owner);
                        EnsureComp<DoorBoltComponent>(door.Owner, out var bolt);
                        var doorEntity = new Entity<DoorBoltComponent>(door.Owner, bolt);
                        _door.TrySetBoltDown(doorEntity, false);
                        door.LockPickProgress = 0;
                        if (TryComp<AffectRoundStatsComponent>(sender.AttachedEntity, out var player))
                        {
                            player.Lockpicks++;
                            foreach (var barrier in EntityManager.EntityQuery<RoundStatCounterRuleComponent>())
                            {
                                barrier.TotalLockpicks++;
                            }
                        }
                        return;
                    }
                }


                if (comp.UseCount != 0)
                    _audio.PlayPvs(new SoundPathSpecifier(comp.EffectSoundOnNo), door.Owner);

                if (!TryComp<DoorHackerComponent>(sender.AttachedEntity, out var doorHacker))
                {
                    if (number > rightNumber)
                    {
                        _prayerSystem.SendSubtleMessage(sender, sender, "Число " + number + ". Вы надавили на отмычку слишком сильно, сброс прогресса взлома", "Взлом провал");
                        door.LockPickProgress = 0;

                    }
                    else if (number < rightNumber)
                    {
                        _prayerSystem.SendSubtleMessage(sender, sender, "Число " + number + ". Вы надавили на отмычку слишком слабо, сброс прогресса взлома", "Взлом провал");
                        door.LockPickProgress = 0;

                    }
                }
                else
                {
                    if (number > rightNumber)
                    {
                        door.LockPickProgress = 0;

                        if (number - 1 == rightNumber || number - 2 == rightNumber)
                            _prayerSystem.SendSubtleMessage(sender, sender, "Число " + number + ". Нужно надавить немного слабее, сброс прогресса взлома", "Взлом провал");
                        else
                            _prayerSystem.SendSubtleMessage(sender, sender, "Число " + number + ". Нужно надавить слабее, сброс прогресса взлома", "Взлом провал");
                    }
                    else if (number < rightNumber)
                    {
                        door.LockPickProgress = 0;

                        if (number + 1 == rightNumber || number + 2 == rightNumber)
                            _prayerSystem.SendSubtleMessage(sender, sender, "Число " + number + ". Нужно надавить немного сильнее, сброс прогресса взлома", "Взлом провал");
                        else
                            _prayerSystem.SendSubtleMessage(sender, sender, "Число " + number + ". Нужно надавить сильнее, сброс прогресса взлома", "Взлом провал");
                    }
                }

                if (comp.UseCount == 0)
                {
                    _prayerSystem.SendSubtleMessage(sender, sender, "Отмычка сломалась", "Взлом провал");
                    if (sender.AttachedEntity != null)
                        _audio.PlayPvs(new SoundPathSpecifier(comp.EffectSoundOnBreak), sender.AttachedEntity.Value);
                    QueueDel(comp.Owner);
                }
            }
            else
            {
                _prayerSystem.SendSubtleMessage(sender, sender, "Неверное значение", "Взлом неверно");
            }
        }
    }
}

using Content.Shared.Nocturn.Components;
using Robust.Shared.Timing;
using Robust.Shared.Map;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Random;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Damage;
using Content.Shared.Popups;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared.Examine;
using Content.Shared.NocturnBitten;
using Robust.Shared.Audio.Systems;
using Content.Shared.Alert;
using Content.Shared.SSDIndicator;
using Content.Shared.Nutrition.Components;
using Robust.Shared.Audio;
using Content.Server.Stunnable;
using Content.Shared.Movement.Systems;
using Content.Server.Chat.Systems;
using Content.Server.NeedSleep.Components;
using Content.Server.Nutrition.Components;
using Content.Shared.Inventory;
using Content.Shared.Chat;
using Content.Shared.Chat.TypingIndicator;

namespace Content.Server.Nocturn
{
    public sealed partial class RaceSystem : EntitySystem
    {
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
        [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly SharedActionsSystem _action = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
        [Dependency] private readonly DamageableSystem _damageableSystem = default!;
        [Dependency] private readonly BloodstreamSystem _blood = default!;
        [Dependency] private readonly AlertsSystem _alerts = default!;
        [Dependency] private readonly StunSystem _stun = default!;
        [Dependency] private readonly ChatSystem _chatSystem = default!;
        [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
        [Dependency] private readonly InventorySystem _inventory = default!;
        [Dependency] private readonly SharedTypingIndicatorSystem _typing = default!;


        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<NocturnComponent, MapInitEvent>(OnComponentInit);
            SubscribeLocalEvent<NocturnComponent, ComponentStartup>(OnStart);
            SubscribeLocalEvent<ZveresScreamComponent, ComponentStartup>(OnZveresStart);
            SubscribeLocalEvent<NocturnBadFoodComponent, ComponentStartup>(OnFoodStart);
            SubscribeLocalEvent<NocturnComponent, NocturnDrinkActionEvent>(OnNocturnDrinkAction);
            SubscribeLocalEvent<ZveresScreamComponent, ZveresScreamActionEvent>(OnZveresScreamAction);
            SubscribeLocalEvent<NocturnComponent, NocturnDrinkDoAfterEvent>(OnNocturnDrinkDoAfter);
            SubscribeLocalEvent<NocturnComponent, NocturnDisguiseActionEvent>(OnNocturnDisguiseAction);
            SubscribeLocalEvent<NocturnComponent, NocturnDisguiseDoAfterEvent>(OnNocturnDisguiseDoAfter);
            SubscribeLocalEvent<NocturnComponent, ExaminedEvent>(OnExamine);
            SubscribeLocalEvent<ZveresScreamComponent, RefreshMovementSpeedModifiersEvent>(OnZveresMove);
        }

        public void OnFoodStart(EntityUid uid, NocturnBadFoodComponent component, ComponentStartup args)
        {
            component.MaxTimesCanBeBiten = component.TimesCanBeBiten;
        }
        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            var zveresquery = EntityQueryEnumerator<ZveresScreamComponent>();
            while (zveresquery.MoveNext(out var uid, out var comp))
            {
                comp.TimeBeforeRemove -= frameTime;
                if (comp.TimeBeforeRemove <= 0)
                    comp.TimeBeforeRemove = 0f;
                _movement.RefreshMovementSpeedModifiers(uid);
            }

            var foodquery = EntityQueryEnumerator<NocturnBadFoodComponent>();
            while (foodquery.MoveNext(out var uid, out var comp))
            {
                if (_timing.CurTime > comp.EndTime)
                {
                    if (TryComp<MobStateComponent>(uid, out var entityMobState) && _mobStateSystem.IsDead(uid, entityMobState))
                    {
                        continue;
                    }
                    comp.StartTime = _timing.CurTime;
                    comp.EndTime = comp.StartTime + TimeSpan.FromSeconds(120f);
                    comp.TimesCanBeBiten++;
                    if (comp.TimesCanBeBiten > comp.MaxTimesCanBeBiten)
                        comp.TimesCanBeBiten = comp.MaxTimesCanBeBiten;

                }
            }
            var activequery = EntityQueryEnumerator<NocturnComponent>();
            while (activequery.MoveNext(out var uid, out var comp))
            {
                comp.FreshDrinkTimer -= frameTime;
                if (_timing.CurTime > comp.EndTime)
                {
                    if (TryComp<MobStateComponent>(uid, out var entityMobState) && _mobStateSystem.IsDead(uid, entityMobState))
                    {
                        continue;
                    }

                    _alerts.ShowAlert(comp.Owner, comp.BloodAlert, (short)Math.Clamp(Math.Round(comp.BloodLevel / 40f), 0, 10));
                    comp.StartTime = _timing.CurTime;

                    comp.EndTime = comp.StartTime + TimeSpan.FromSeconds(1f);

                    if (comp.BloodLevel > 400f)
                    {
                        comp.BloodLevel = 400f;
                    }

                    if (comp.BloodLevel >= 200f && TryComp<DamageableComponent>(comp.Owner, out var damageable) && damageable.TotalDamage < 61f && damageable.TotalDamage > 5f)
                    {
                        _damageableSystem.TryChangeDamage(uid, -comp.BloodLostDamage, true, false);
                        //comp.BloodLevel -= comp.BloodDrainPerSecond * 2;
                    }

                    if (comp.BloodLevel >= 200f && TryComp<DamageableComponent>(comp.Owner, out var damag) && damag.TotalDamage < 105f && damag.TotalDamage > 60f)
                    {
                        _damageableSystem.TryChangeDamage(uid, -comp.BloodLostDamage, true, false);
                        //_damageableSystem.TryChangeDamage(uid, -comp.RegenDamage * 3.5f, true, false);
                        //comp.BloodLevel -= comp.BloodDrainPerSecond * 39;
                    }

                    if (comp.BloodDrainPerSecond > comp.BloodLevel)
                    {
                        comp.BloodLevel = 0f;
                    }
                    else
                    {
                        comp.BloodLevel -= comp.BloodDrainPerSecond;
                    }


                    if (comp.BloodLevel < 50f && comp.BloodLevel != 0f)
                    {
                        if (_random.Prob(0.1f))
                        {
                            _popupSystem.PopupEntity("Низкий уровень крови", uid, uid, PopupType.MediumCaution);
                        }
                        _damageableSystem.TryChangeDamage(uid, comp.BloodLostDamage, true, false);
                    }

                    else if (comp.BloodLevel == 0f)
                    {
                        if (_random.Prob(0.25f))
                        {
                            _popupSystem.PopupEntity("КРОВЬ!!", uid, uid, PopupType.LargeCaution);
                        }

                        _damageableSystem.TryChangeDamage(uid, comp.BloodLostDamage * 3f, true, false);
                    }

                    if (comp.BloodLevel < 50f && comp.IsDisguised)
                    {
                        if (TryComp<HumanoidAppearanceComponent>(uid, out var appearance))
                        {
                            _popupSystem.PopupEntity(Loc.GetString("nocturn-disguise-low-blood"), uid, uid, PopupType.LargeCaution);

                            _audio.PlayPvs(new SoundPathSpecifier(comp.EffectSoundOnDisguise), uid);
                            RevertToOriginalForm(uid, comp, appearance);
                        }
                    }
                }
            }
        }

        private void OnComponentInit(Entity<NocturnComponent> ent, ref MapInitEvent args)
        {
            _action.AddAction(ent.Owner, ref ent.Comp.DisguiseActionEntity, ent.Comp.DisguiseAction, ent.Owner);
        }

        public void OnZveresStart(EntityUid uid, ZveresScreamComponent component, ComponentStartup args)
        {
            _action.AddAction(uid, "ZveresScreamAction", uid);
        }
        public void OnStart(EntityUid uid, NocturnComponent component, ComponentStartup args)
        {
            if (TryComp<HungerComponent>(uid, out var hunger))
            {
                EntityManager.RemoveComponent<HungerComponent>(hunger.Owner);
            }
            if (TryComp<ThirstComponent>(uid, out var thirst))
            {
                EntityManager.RemoveComponent<ThirstComponent>(thirst.Owner);
            }

            _action.AddAction(uid, "NocturnDrinkAction", uid);
        }

        public void OnZveresScreamAction(EntityUid uid, ZveresScreamComponent comp, ZveresScreamActionEvent args)
        {
            args.Handled = true;
            comp.TimeBeforeRemove += 4f;
            _chatSystem.TryEmoteWithChat(uid, "Scream", ChatTransmitRange.Normal);
            if (TryComp<NeedSleepComponent>(uid, out var needSleep))
                needSleep.SleepLevel += 18f;
        }

        private void OnZveresMove(EntityUid uid, ZveresScreamComponent component, RefreshMovementSpeedModifiersEvent args)
        {
            if (component.TimeBeforeRemove > 0)
            {
                args.ModifySpeed(1.26f, 1.26f);
            }
            else
            {
                args.ModifySpeed(1f, 1f);
            }
        }
        public void OnNocturnDrinkAction(EntityUid uid, NocturnComponent component, NocturnDrinkActionEvent args)
        {
            IngestionBlockerComponent? blocker;

            if (_inventory.TryGetSlotEntity(uid, "mask", out var maskUid) &&
                TryComp(maskUid, out blocker) &&
                blocker.Enabled)
            {
                _popupSystem.PopupEntity("Ваш рот чем-то закрыт", uid, uid, PopupType.Large);
                return;
            }

            if (_inventory.TryGetSlotEntity(uid, "head", out var headUid) &&
                TryComp(headUid, out blocker) &&
                blocker.Enabled)
            {
                _popupSystem.PopupEntity("Ваш рот чем-то закрыт", uid, uid, PopupType.Large);
                return;
            }



            var target = args.Target;
            if (!CanBite(uid))
            {
                _popupSystem.PopupEntity("Рядом чеснок, библия или крест", uid, uid, PopupType.Large);
                return;
            }

            var doAfterEventArgs = new DoAfterArgs(EntityManager, uid, 2f, new NocturnDrinkDoAfterEvent(), uid, target: target, used: uid)
            {
                BreakOnMove = true,
                BreakOnDamage = false,
                NeedHand = false
            };
            if (TryComp<NocturnBadFoodComponent>(target, out var food) && !food.Fresh)
                _popupSystem.PopupEntity("Какая грязная кровь... мерзко.", uid, uid, PopupType.Large);
            _doAfterSystem.TryStartDoAfter(doAfterEventArgs);
        }

        private void OnNocturnDrinkDoAfter(EntityUid uid, NocturnComponent component, NocturnDrinkDoAfterEvent args)
        {
            if (args.Handled || args.Cancelled)
                return;

            if (args.Args.Target is not null)
            {
                if (TryComp<BloodstreamComponent>(args.Args.Target, out var bloodstream))
                {
                    if (TryComp<SSDIndicatorComponent>(args.Args.Target, out var ssd) && ssd.IsSSD)
                    {
                        {
                            _popupSystem.PopupEntity("Его кровь безжизненная", uid, uid, PopupType.Large);
                            return;
                        }
                    }
                    if (TryComp<NocturnBadFoodComponent>(args.Args.Target, out var food) && !food.Fresh)
                    {
                        if (food.TimesCanBeBiten > 0)
                        {
                            food.TimesCanBeBiten -= 1;
                            component.DrinkAnimals++;
                            _blood.TryModifyBloodLevel(args.Args.Target.Value, -25, bloodstream);
                            component.BloodLevel += 30f * food.BloodMultiplier;
                            var xform = Transform(component.Owner);
                            var coords = xform.Coordinates;
                            _popupSystem.PopupCoordinates(Loc.GetString("Пьет кровь"), coords, PopupType.MediumCaution);
                            var txform = Transform(args.Args.Target.Value);
                            var tcoords = txform.Coordinates;
                            Spawn("BloodParticles", tcoords);
                            _damageableSystem.TryChangeDamage(component.Owner, -component.RegenDamage * 15 * food.BloodMultiplier, true, false);
                            component.FreshDrinkTimer = 60f;
                            if (!HasComp<NocturnBittenComponent>(args.Args.Target))
                            {
                                AddComp<NocturnBittenComponent>(args.Args.Target.Value);
                            }
                            _audio.PlayPvs(new SoundPathSpecifier(component.EffectSoundOnDrink), component.Owner);
                            return;
                        }
                        else
                        {
                            _popupSystem.PopupEntity("Цель иссушена", uid, uid, PopupType.Large);
                            return;
                        }
                    }
                    if (HasComp<HumanoidAppearanceComponent>(args.Args.Target.Value))
                    {
                        if (!HasComp<NocturnComponent>(args.Args.Target))
                        {
                            if (TryComp<NocturnBadFoodComponent>(args.Args.Target, out var badfood))
                            {
                                if (badfood.TimesCanBeBiten > 0)
                                {
                                    badfood.TimesCanBeBiten -= 1;
                                    component.DrinkHumans++;
                                }
                                else
                                {
                                    _popupSystem.PopupEntity("Цель иссушена", uid, uid, PopupType.Large);
                                    return;
                                }
                            }
                            ShowEyes(uid);
                            _blood.TryModifyBloodLevel(args.Args.Target.Value, -25, bloodstream);
                            component.BloodLevel += 30f;
                            var xform = Transform(component.Owner);
                            var coords = xform.Coordinates;
                            _popupSystem.PopupCoordinates(Loc.GetString("Пьет кровь"), coords, PopupType.MediumCaution);
                            var txform = Transform(args.Args.Target.Value);
                            var tcoords = txform.Coordinates;
                            Spawn("BloodParticles", tcoords);
                            _damageableSystem.TryChangeDamage(component.Owner, -component.RegenDamage * 3, true, false);
                            component.FreshDrinkTimer = 60f;
                            if (!HasComp<NocturnBittenComponent>(args.Args.Target))
                            {
                                AddComp<NocturnBittenComponent>(args.Args.Target.Value);
                            }
                            _audio.PlayPvs(new SoundPathSpecifier(component.EffectSoundOnDrink), component.Owner);
                        }
                        else
                        {
                            _popupSystem.PopupEntity("Нельзя пить кровь ноктюрна", uid, uid, PopupType.Large);
                        }
                    }
                    else
                    {
                        _popupSystem.PopupEntity("У него нельзя сейчас выпить кровь", uid, uid, PopupType.Large);
                    }
                }
            }
        }

        public void OnNocturnDisguiseAction(EntityUid uid, NocturnComponent component, NocturnDisguiseActionEvent args)
        {
            if (!CanBite(uid))
            {
                _popupSystem.PopupEntity(Loc.GetString("nocturn-disguise-obstacle"), uid, uid, PopupType.Large);
                return;
            }

            var doAfterArgs = new DoAfterArgs(EntityManager, uid, 2.25f, new NocturnDisguiseDoAfterEvent(), uid)
            {
                BreakOnMove = false,
                BreakOnDamage = false,
                NeedHand = false
            };

            _audio.PlayPvs(new SoundPathSpecifier(component.EffectSoundOnDisguise), uid);
            _doAfterSystem.TryStartDoAfter(doAfterArgs);
            args.Handled = true;
        }

        private void OnNocturnDisguiseDoAfter(EntityUid uid, NocturnComponent component, NocturnDisguiseDoAfterEvent args)
        {
            if (args.Handled || args.Cancelled)
                return;

            if (!TryComp<HumanoidAppearanceComponent>(uid, out var appearance))
                return;

            if (!component.IsDisguised)
            {
                if (component.BloodLevel < 50)
                {
                    _popupSystem.PopupEntity(Loc.GetString("nocturn-disguise-low-blood"), uid, uid, PopupType.Large);
                    return;
                }

                _popupSystem.PopupEntity(Loc.GetString("nocturn-disguise-apply"), uid, uid);
                ApplyDisguise(uid, component, appearance);
            }
            else
            {
                _popupSystem.PopupEntity(Loc.GetString("nocturn-disguise-revert"), uid, uid);
                RevertToOriginalForm(uid, component, appearance);
            }
        }

        private void ApplyDisguise(EntityUid uid, NocturnComponent component, HumanoidAppearanceComponent appearance)
        {
            appearance.Species = "Human";
            component.BloodDrainPerSecond *= 2;
            component.BloodLevel -= 10;

            component.IsDisguised = true;
            _action.SetToggled(component.DisguiseActionEntity, component.IsDisguised);
            Dirty(uid, appearance);
            if (TryComp<TypingIndicatorComponent>(uid, out var typing))
            {
                typing.TypingIndicatorPrototype = component.TypingIndicatorPrototypeBase;
                Dirty(uid, typing);
            }
        }

        private void RevertToOriginalForm(EntityUid uid, NocturnComponent component, HumanoidAppearanceComponent appearance)
        {
            appearance.Species = "Drou";
            component.BloodDrainPerSecond /= 2;

            component.IsDisguised = false;
            _action.SetToggled(component.DisguiseActionEntity, component.IsDisguised);
            Dirty(uid, appearance);
            if (TryComp<TypingIndicatorComponent>(uid, out var typing))
            {
                typing.TypingIndicatorPrototype = component.TypingIndicatorPrototypeMod;
                Dirty(uid, typing);
            }
        }

        public void ShowEyes(EntityUid uid)
        {
            if (TryComp<HumanoidAppearanceComponent>(uid, out var appeareance))
            {
                appeareance.EyeColor = Color.Red;
                Dirty(uid, appeareance);
            }

        }

        private void OnExamine(EntityUid uid, NocturnComponent component, ExaminedEvent args)
        {
            if (component.BloodLevel < 50)
            {
                args.PushMarkup("[color=red]Во рту видны клыки[/color]");
            }
            if (component.BloodLevel < 5)
            {
                args.PushMarkup("[color=red]Тяжело дышит[/color]");
            }
            if (component.FreshDrinkTimer > 0)
            {
                args.PushMarkup("[color=red]На губах видна свежая кровь[/color]");
            }

        }

        public bool CanBite(EntityUid vampire)
        {
            foreach (var target in _lookup.GetEntitiesInRange(vampire, 5.5f))
            {
                if (HasComp<NocturnBlockBiteComponent>(target))
                {
                    return false;
                }
            }
            return true;
        }
    }
}

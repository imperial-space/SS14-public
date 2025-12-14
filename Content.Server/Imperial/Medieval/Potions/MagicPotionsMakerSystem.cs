using Content.Server.MagicPotionsMaker.Components;
using Robust.Shared.Timing;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Content.Shared.Examine;
using Robust.Shared.Random;
using Content.Shared.Interaction;
using Content.Shared.Random.Helpers;
using Robust.Shared.Audio;
using Content.Server.SpikeTrap.Components;
using Content.Server.MagicBarrier.Components;
using Content.Server.Imperial.Medieval.GameTicking.Rules;
using Content.Shared.Imperial.Medieval.GameTicking.Rules;

namespace Content.Server.MagicPotionsMaker
{
    public sealed partial class MagicPotionsMakerSystem : EntitySystem
    {
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly IRobustRandom _random = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MagicPotionsMakerComponent, ExaminedEvent>(OnExamine);
            SubscribeLocalEvent<MagicPotionsIngredientComponent, ExaminedEvent>(OnExamineIngredient);
            SubscribeLocalEvent<MagicPotionsMakerComponent, ActivateInWorldEvent>(OnActivated);
            SubscribeLocalEvent<MagicPotionsRecipesComponent, ComponentStartup>(MixRecipes);
            SubscribeLocalEvent<MagicPotionsIngredientComponent, BeforeRangedInteractEvent>(OnUseInHand);
        }

        public void OnUseInHand(EntityUid uid, MagicPotionsIngredientComponent comp, BeforeRangedInteractEvent args)
        {
            if (!args.CanReach)
                return;
            OnUse(args.Target, args.User, args.Used, comp);
        }

        public void OnUse(EntityUid? target, EntityUid user, EntityUid used, MagicPotionsIngredientComponent ingredient)
        {
            if (target == null)
                return;

            if (TryComp<MagicPotionsMakerComponent>(target, out var maker))
            {
                if (maker.FirstIngredient == "None")
                {
                    maker.Charge += 1f;
                    maker.FirstIngredient = ingredient.IngredientType;
                    _audio.PlayPvs(new SoundPathSpecifier(maker.EffectSoundOnAddingIngredient), target.Value);
                    QueueDel(used);
                }
                else
                {
                    if (maker.SecondIngredient == "None")
                    {
                        maker.Charge += 1f;
                        maker.SecondIngredient = ingredient.IngredientType;
                        _audio.PlayPvs(new SoundPathSpecifier(maker.EffectSoundOnAddingIngredient), target.Value);
                        QueueDel(used);
                    }
                }
            }

        }
        public void MixRecipes(EntityUid uid, MagicPotionsRecipesComponent component, ComponentStartup args)
        {
            component.CryoCryo = _random.Pick(component.Potions);
            component.CryoVetr = _random.Pick(component.Potions);
            component.CryoLipad = _random.Pick(component.Potions);
            component.CryoLava = _random.Pick(component.Potions);

            component.VetrVetr = _random.Pick(component.Potions);
            component.VetrLipad = _random.Pick(component.Potions);
            component.VetrLava = _random.Pick(component.Potions);

            component.LipadLipad = _random.Pick(component.Potions);
            component.LipadLava = _random.Pick(component.Potions);

            component.LavaLava = _random.Pick(component.Potions);

        }

        public void OnActivated(EntityUid uid, MagicPotionsMakerComponent comp, ActivateInWorldEvent args)
        {
            if (comp.FirstIngredient == "None" || comp.SecondIngredient == "None")
                return;
            // Cryo Vetr Lipad Lava
            CookPotion(uid, comp, comp.FirstIngredient + comp.SecondIngredient);
            if (TryComp<AffectRoundStatsComponent>(args.User, out var player))
            {
                player.Potions++;
                foreach (var barrier in EntityManager.EntityQuery<RoundStatCounterRuleComponent>())
                {
                    barrier.TotalPotions++;
                }
            }
        }

        public void CookPotion(EntityUid uid, MagicPotionsMakerComponent component, string Reagents)
        {
            _audio.PlayPvs(new SoundPathSpecifier(component.EffectSoundOnFinish), uid);
            component.FirstIngredient = "None";
            component.SecondIngredient = "None";
            component.Charge = 0f;
            var xform = Transform(uid);
            var coords = xform.Coordinates;
            foreach (var resipecomp in EntityManager.EntityQuery<MagicPotionsRecipesComponent>())
            {
                // Cryo Vetr Lipad Lava
                switch (Reagents)
                {
                    case "CryoCryo":
                        Spawn(resipecomp.CryoCryo, coords);
                        break;
                    case "CryoVetr":
                    case "VetrCryo":
                        Spawn(resipecomp.CryoVetr, coords);
                        break;
                    case "CryoLipad":
                    case "LipadCryo":
                        Spawn(resipecomp.CryoLipad, coords);
                        break;
                    case "CryoLava":
                    case "LavaCryo":
                        Spawn(resipecomp.CryoLava, coords);
                        break;

                    case "VetrVetr":
                        Spawn(resipecomp.VetrVetr, coords);
                        break;
                    case "VetrLipad":
                    case "LipadVetr":
                        Spawn(resipecomp.VetrLipad, coords);
                        break;
                    case "VetrLava":
                    case "LavaVetr":
                        Spawn(resipecomp.VetrLava, coords);
                        break;

                    case "LipadLipad":
                        Spawn(resipecomp.LipadLipad, coords);
                        break;
                    case "LipadLava":
                    case "LavaLipad":
                        Spawn(resipecomp.LipadLava, coords);
                        break;

                    case "LavaLava":
                        Spawn(resipecomp.LavaLava, coords);
                        break;
                }
            }

        }

        private void OnExamine(EntityUid uid, MagicPotionsMakerComponent component, ExaminedEvent args)
        {
            args.PushMarkup("[color=cyan]Загружено " + Math.Round(component.Charge, 2) + " игредиентов из " + Math.Round(component.MaxCharge, 2) + "[/color]");
        }
        private void OnExamineIngredient(EntityUid uid, MagicPotionsIngredientComponent component, ExaminedEvent args)
        {
            args.PushMarkup("[color=cyan]Это алхимический ингредиент [/color]");
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            foreach (var comp in EntityManager.EntityQuery<MagicPotionsMakerComponent>())
            {
                if (_timing.CurTime > comp.EndTime)
                {
                    comp.StartTime = _timing.CurTime;
                    comp.EndTime = comp.StartTime + comp.ReloadTime;
                    var xform = Transform(comp.Owner);
                    var coords = xform.Coordinates;

                }
            }
        }
    }
}

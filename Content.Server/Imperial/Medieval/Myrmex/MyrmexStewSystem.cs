using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.Myrmex;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Examine;

namespace Content.Server.Imperial.Medieval.Myrmex
{
    public sealed partial class MyrmexStewSystem : EntitySystem
    {
        [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        private readonly string[] _feedSounds = new[] {
                "/Audio/Imperial/Medieval/ant_feed1.ogg",
                "/Audio/Imperial/Medieval/ant_feed2.ogg",
            };

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MyrmexStewComponent, UseInHandEvent>(OnUseInHand);
            SubscribeLocalEvent<MyrmexStewComponent, ActivateInWorldEvent>(OnInteractHand);
            SubscribeLocalEvent<MyrmexStewComponent, StewFeedDoAfterEvent>(OnDoAfter);
            SubscribeLocalEvent<MyrmexStewComponent, ExaminedEvent>(OnExamine);
        }

        private void OnExamine(EntityUid uid, MyrmexStewComponent comp, ExaminedEvent args)
        {
            args.PushMarkup($"[color=gray]Осталось [bold]{comp.Uses}[/bold] кусочков[/color]", 1);
        }
        private void OnInteractHand(Entity<MyrmexStewComponent> entity, ref ActivateInWorldEvent args)
        {
            if (args.Handled)
                return;

            if (!TryComp<MyrmexHungerComponent>(args.User, out var hunger))
                return;

            var userEntity = new Entity<MyrmexHungerComponent>(args.User, hunger);

            var result = TryFeed(entity, userEntity);
            args.Handled = result.Handled;
        }

        private void OnUseInHand(Entity<MyrmexStewComponent> entity, ref UseInHandEvent args)
        {
            if (args.Handled)
                return;

            if (!TryComp<MyrmexHungerComponent>(args.User, out var hunger))
                return;

            var userEntity = new Entity<MyrmexHungerComponent>(args.User, hunger);

            var result = TryFeed(entity, userEntity);
            args.Handled = result.Handled;
        }

        private bool FeedCheck(Entity<MyrmexStewComponent> stew, Entity<MyrmexHungerComponent> user, bool silent = false)
        {
            var curTime = _gameTiming.CurTime;

            var diff = (curTime - user.Comp.LastEaten);

            if (HasComp<LarvaComponent>(user.Owner) && !stew.Comp.EdibleByLarva)
                return false;

            if(diff.HasValue && diff.Value.Duration() < TimeSpan.FromSeconds(user.Comp.EatCooldownSeconds))
            {
                if(!silent)
                    _popup.PopupEntity(Loc.GetString("medieval-myrmex-stew-cooldown"), user.Owner, user.Owner);
                return false;
            }

            return true;
        }

        private (bool Success, bool Handled) TryFeed(Entity<MyrmexStewComponent> stew, Entity<MyrmexHungerComponent> user)
        {
            if (!FeedCheck(stew, user))
                return (false, true);


            var sound = _random.Pick(_feedSounds);
            _audio.PlayPvs(sound, user.Owner);
            var doAfterArgs = new DoAfterArgs(EntityManager,
                user.Owner,
                TimeSpan.FromSeconds(2),
                new StewFeedDoAfterEvent(),
                target: stew.Owner,
                eventTarget: stew.Owner,
                used: stew.Owner
            )
            {
                BreakOnDamage = true,
                CancelDuplicate = true,
                DistanceThreshold = 2,
                BreakOnMove = true,
            };
            _doAfter.TryStartDoAfter(doAfterArgs);
            return (true, true);
        }

        private void OnDoAfter(Entity<MyrmexStewComponent> entity, ref StewFeedDoAfterEvent args)
        {
            if (args.Cancelled || args.Handled || entity.Comp.Deleted || args.Target == null)
                return;

            if (!TryComp(args.User, out MyrmexHungerComponent? hunger))
                return;
            if (!TryComp(entity.Owner, out MetaDataComponent? metadata))
                return;
            if (!TryComp(entity.Owner, out TransformComponent? transform))
                return;

            args.Handled = true;

            var userEntity = new Entity<MyrmexHungerComponent>(args.User, hunger);
            if (!FeedCheck(entity, userEntity))
                return;

            entity.Comp.Uses--;

            hunger.LastEaten = _gameTiming.CurTime;
            if(entity.Comp.Buff != null)
                hunger.Buffs.Add(entity.Comp.Buff);

            hunger.Dirty();

            if (TryComp<LarvaComponent>(args.User, out var larva))
            {
                if (metadata.EntityPrototype == null)
                    return;
                var ev = new LarvaFeedEvent(metadata.EntityPrototype);
                RaiseLocalEvent(args.User, ev);
            }

            if (entity.Comp.Uses == 0)
                PredictedQueueDel(new Entity<MetaDataComponent?, TransformComponent?>(entity.Owner, metadata, transform));
        }
    }
}

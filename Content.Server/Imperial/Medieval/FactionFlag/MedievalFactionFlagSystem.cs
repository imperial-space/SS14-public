using Content.Server.MedievalFactionFlag.Components;
using Content.Shared.MedievalFactionFlag.Components;
using Content.Shared.Examine;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Content.Shared.Interaction;
using Robust.Shared.Timing;
using Content.Shared.DoAfter;

namespace Content.Server.MedievalFactionFlag
{
    public sealed partial class MedievalFactionFlagSystem : EntitySystem
    {
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem Audio = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MedievalFactionFlagCheckerComponent, ExaminedEvent>(OnExamine);
            SubscribeLocalEvent<MedievalFactionFlagPaintComponent, ExaminedEvent>(OnPaintExamine);
            SubscribeLocalEvent<MedievalFactionFlagPaintComponent, BeforeRangedInteractEvent>(OnUseInHand);

        }

        public void OnUseInHand(EntityUid uid, MedievalFactionFlagPaintComponent comp, BeforeRangedInteractEvent args)
        {
            if (!args.CanReach)
                return;
            OnUse(args.Target, args.User, args.Used);
        }

        public void OnUse(EntityUid? target, EntityUid user, EntityUid used)
        {
            if (target == null)
                return;
            if (TryComp<MedievalFactionFlagComponent>(target, out var flagcomp) && flagcomp != null)
            {
                var flag = target.Value;
                if (TryComp<MedievalFactionFlagPaintComponent>(used, out var paint) && paint.Faction != flagcomp.Faction)
                {
                    var xform = Transform(flagcomp.Owner);
                    var coords = xform.Coordinates;
                    QueueDel(flagcomp.Owner);
                    Spawn(paint.Faction, coords);
                    paint.Uses -= 1;
                    if (paint.Uses <= 0)
                        QueueDel(paint.Owner);

                }
            }
        }

        private void OnExamine(EntityUid uid, MedievalFactionFlagCheckerComponent component, ExaminedEvent args)
        {
            var legion = 0;
            var insurgency = 0;
            var none = 0;
            foreach (var barrier in EntityManager.EntityQuery<MedievalFactionFlagComponent>())
            {
                switch (barrier.Faction)
                {
                    case "legion":
                        legion += 1;
                        break;
                    case "insurgency":
                        insurgency += 1;
                        break;
                    case "none":
                        none += 1;
                        break;
                }
            }
            args.PushMarkup("[color=cyan]" + Loc.GetString("flag-controls-legion") + " " + legion + "[/color]");
            args.PushMarkup("[color=red]Точек, подконтрольно мятежникам: " + insurgency + "[/color]");
            args.PushMarkup("[color=white]Свободных точек: " + none + "[/color]");
        }

        private void OnPaintExamine(EntityUid uid, MedievalFactionFlagPaintComponent component, ExaminedEvent args)
        {
            args.PushMarkup("[color=green]Осталось использований: " + component.Uses + "[/color]");
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            foreach (var comp in EntityManager.EntityQuery<MedievalFactionFlagCheckerComponent>())
            {
                if (_timing.CurTime > comp.EndTime)
                {
                    comp.StartTime = _timing.CurTime;
                    comp.EndTime = comp.StartTime + comp.ReloadTime;
                    var xform = Transform(comp.Owner);
                    var coords = xform.Coordinates;
                    if (IsPaused(comp.Owner) || !comp.Enabled)
                        continue;
                    foreach (var barrier in EntityManager.EntityQuery<MedievalFactionFlagComponent>())
                    {
                        if (barrier.Faction == comp.Faction)
                        {
                            Spawn("MedievalRevent25", coords);
                            if (comp.Faction == "legion")
                                Spawn("MedievalMedalLegion", coords);
                            if (comp.Faction == "insurgency")
                                Spawn("MedievalMedalIns", coords);
                        }
                    }
                    switch (comp.Faction)
                    {
                        case "legion":
                            Spawn("MedievalFlagPaintLegion", coords);
                            break;
                        case "insurgency":
                            Spawn("MedievalFlagPaintInsurgency", coords);
                            break;
                    }

                }
            }
        }

    }
}

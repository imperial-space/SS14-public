using Content.Server.MedievalTradeCurse.Components;
using Robust.Shared.Timing;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Content.Shared.Store.Components;
using Content.Server.Polymorph.Systems;
using Content.Shared.Alert;
using Content.Server.SSDFree.Components;
using Content.Shared.SSDFree.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Item;
using Content.Shared.Imperial.Medieval.Trading;

namespace Content.Server.MedievalTradeCurse
{
    public sealed partial class MedievalTradeCurseSystem : EntitySystem
    {
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem Audio = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly PolymorphSystem _polymorphSystem = default!;
        [Dependency] private readonly AlertsSystem _alerts = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;


        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<TraderCurseHealComponent, BeforeRangedInteractEvent>(OnUseInHand);

        }

        public void OnUseInHand(EntityUid uid, TraderCurseHealComponent comp, BeforeRangedInteractEvent args)
        {
            if (!args.CanReach)
                return;
            OnUse(args.Target, args.User, args.Used);
        }

        public void OnUse(EntityUid? target, EntityUid user, EntityUid used)
        {
            if (target == null)
                return;
            if (TryComp<MedievalTradeCurseComponent>(target, out var curse) && curse != null)
            {
                curse.CurseLevel += 20f;
                QueueDel(used);
                _alerts.ShowAlert(curse.Owner, curse.CurseAlert, (short)Math.Clamp(Math.Round(curse.CurseLevel / curse.CurseMax * 5.05f), 0, 5));
                if (curse.CurseLevel >= curse.CurseMax)
                    curse.CurseLevel = curse.CurseMax;

            }
        }



        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            foreach (var comp in EntityManager.EntityQuery<MedievalTradeCurseComponent>())
            {
                if (_timing.CurTime > comp.EndTime)
                {
                    comp.StartTime = _timing.CurTime;
                    comp.EndTime = comp.StartTime + comp.ReloadTime;
                    var xform = Transform(comp.Owner);
                    var coords = xform.Coordinates;
                    _alerts.ShowAlert(comp.Owner, comp.CurseAlert, (short)Math.Clamp(Math.Round(comp.CurseLevel / comp.CurseMax * 5.05f), 0, 5));
                    if (CheckPlayersNearby(coords))
                    {
                        comp.CurseLevel += 3f;
                        if (comp.CurseLevel >= comp.CurseMax)
                            comp.CurseLevel = comp.CurseMax;
                    }
                    else
                    {
                        comp.CurseLevel -= 1f;
                        if (comp.CurseLevel < 15f && comp.CurseLevel > 0f)
                            _popup.PopupEntity("Нужно быть ближе к торговой дыре", comp.Owner, comp.Owner, PopupType.LargeCaution);
                        if (comp.CurseLevel <= 0f)
                        {
                            _popup.PopupEntity("СРОЧНО к торговой дыре!!", comp.Owner, comp.Owner, PopupType.LargeCaution);
                            comp.CurseLevel = 0f;
                            if (TryComp<SSDFreeComponent>(comp.Owner, out var ssdfree))
                            {
                                ssdfree.UnholyValue += ssdfree.UnholySpeed * 3;
                            }
                        }

                    }

                }
            }
        }
        public bool CheckPlayersNearby(EntityCoordinates coords)
        {

            foreach (var entity in _lookup.GetEntitiesInRange(coords, 3.5f))
            {
                if (HasComp<TradingComponent>(entity) && !HasComp<ItemComponent>(entity))
                {
                    return true;
                }

            }
            return false;
        }

    }

}

using Content.Server.MagicSpellcraft.Components;
using Robust.Shared.Timing;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Content.Shared.Examine;
using Content.Server.MagicBarrier.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Audio;
using Content.Shared.Alert;

namespace Content.Server.MagicSpellcraft
{
    public sealed partial class MagicSpellcraftSystem : EntitySystem
    {
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem Audio = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly AlertsSystem _alerts = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<MagicSpellcraftComponent, ExaminedEvent>(OnExamine);
        }

        private void OnExamine(EntityUid uid, MagicSpellcraftComponent component, ExaminedEvent args)
        {
            args.PushMarkup("[color=red]Текущий заряд " + Math.Round(component.Charge, 2) + " из " + Math.Round(component.MaxCharge, 2) + "[/color]");
        }
        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            foreach (var comp in EntityManager.EntityQuery<MagicSpellcraftComponent>())
            {
                if (_timing.CurTime > comp.EndScrollTime)
                {
                    comp.StartScrollTime = _timing.CurTime;
                    comp.EndScrollTime = comp.StartScrollTime + comp.ReloadScrollTime;
                    var xform = Transform(comp.Owner);
                    var coords = xform.Coordinates;

                }

                if (_timing.CurTime > comp.EndTime)
                {
                    comp.StartTime = _timing.CurTime;
                    comp.EndTime = comp.StartTime + comp.ReloadTime;
                    var xform = Transform(comp.Owner);
                    var coords = xform.Coordinates;


                    if (comp.Charge >= comp.MaxCharge)
                    {

                        comp.Charge = 0f;
                        Audio.PlayPvs(new SoundPathSpecifier(comp.EffectSoundOnFinish), comp.Owner);
                        Spawn(comp.SpawnedEntity, coords);

                    }
                }
            }
        }
    }
}

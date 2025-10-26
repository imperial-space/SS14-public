using Robust.Server.GameObjects;
using Robust.Shared.Timing;
using Content.Server.Imperial.Halloween.Components;
using System.ComponentModel;

namespace Content.Server.Imperial.Halloween
{
    public sealed class JackPumpkinSystem : EntitySystem
    {
        [Dependency] private readonly IGameTiming _timing = default!;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<JackPumpkinComponent, ComponentStartup>(GetDelayTime);
        }
        private void GetDelayTime(EntityUid uid, JackPumpkinComponent component, ref ComponentStartup args)
        {
            component.Delay = _timing.CurTime + component.Delay;
            component.GetDelay = true;
        }
        private void RaiseEvent(EntityUid uid, JackPumpkinComponent component)
        {
            component.EventRaised = true;
            var ev = new JackSpawnedEvent();
            RaiseLocalEvent(ev);
        }
        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<JackPumpkinComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                if (comp.GetDelay)
                {
                    if (_timing.CurTime > comp.Delay && !comp.EventRaised)
                    {
                        RaiseEvent(uid, comp);
                    }
                }
            }
        }
    }
}

using Content.Shared.Humanoid;
using Content.Shared.Imperial.PinpointerCritical.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Pinpointer;
using Robust.Server.GameObjects;

namespace Content.Server.Imperial.PinpointerCritical.Systems;

public sealed partial class PinpointerCriticalSystem : EntitySystem
{
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedPinpointerSystem _pinpointer = default!;
    //[Dependency] private readonly ISawmill _sawmill = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PinpointerCriticalComponent, ActivateInWorldEvent>(OnSwitch);
    }
    public void OnSwitch(EntityUid uid, PinpointerCriticalComponent comp, ActivateInWorldEvent ev)
    {
        var userCoords = _transform.GetMapCoordinates(uid); // Координаты игрока
        EntityUid? closestUid = null;
        var closestDistance = float.MaxValue;

        var query = EntityQueryEnumerator<MobThresholdsComponent>();
        while (query.MoveNext(out var uidd, out var compp))
        {
            if (HasComp<HumanoidAppearanceComponent>(uidd) && compp.CurrentThresholdState == MobState.Critical
             && Transform(ev.User).ParentUid == Transform(uidd).ParentUid) // Проверка на критическое состояние
            {
                var targetCoords = _transform.GetMapCoordinates(uidd); // Координаты цели
                var distance = (float)(targetCoords.Position - userCoords.Position).LengthSquared(); // Квадрат расстояния

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestUid = uidd;
                }

            }
        }

        if (closestUid != null)
        {
            //_sawmill.Info($"Ближайший критический моб: {ToPrettyString(closestUid)} на расстоянии {MathF.Sqrt(closestDistance)}: вызвано {ToPrettyString(uid)}");
        }
        else
        {
            //_sawmill.Info($"Критических мобов не найдено: вызвано {ToPrettyString(uid)}");
        }
        if (TryComp<PinpointerComponent>(ev.Target, out var pinComp) && closestUid != null)
        {
            _pinpointer.SetTarget(uid, closestUid, pinComp);
        }
    }

}

using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenomorph;

/// <summary>
/// Кислота (component/acid, acid_power 200, acid_volume 1000). Предмет получает
/// min(1 + sqrt(power × volume) × 0.3, 300) урона в секунду, объём убывает на 1 + sqrt(volume) в секунду.
/// Обычная стена плавится за 30 секунд, укреплённые не плавятся.
/// </summary>
[RegisterComponent]
public sealed partial class XenoAcidComponent : Component
{
    [DataField]
    public float Power = 200;

    [DataField]
    public float Volume = 300;

    [ViewVariables] public bool Wall;
    [ViewVariables] public float WallIntegrity = 30;
    [ViewVariables] public TimeSpan NextTick;
    [ViewVariables] public EntityUid? Overlay;
}

using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Changeling;

/// <summary>Жертва трансформирующего жала: через 8 минут (пока жива) вернётся в свой облик.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingStungTransformComponent : Component
{
    /// <summary>Копия исходного облика на паузной карте.</summary>
    [ViewVariables]
    public EntityUid? Original;

    /// <summary>Осталось; INFINITY для низших форм.</summary>
    [ViewVariables]
    public TimeSpan? Remaining;
}

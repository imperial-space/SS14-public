using Content.Shared.Actions;
using Content.Shared.Construction;
using Content.Shared.DoAfter;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Voidwalker;

/// <summary>/datum/component/glass_passer: сквозь стёкла и решётки (кроме электрифицированных).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialGlassPasserComponent : Component
{
    /// <summary>pass_time: 0 — проходит свободно, иначе нужно «продавиться» сквозь стекло.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan PassTime = TimeSpan.Zero;

    /// <summary>deform_glass: сколько пройденное стекло остаётся проходимым для всех.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan DeformGlass = TimeSpan.FromSeconds(5);

    /// <summary>До этого момента стекло пропускает (после продавливания).</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan PhasingUntil;

    [DataField]
    public TimeSpan UpdateDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan PhasingDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public float UpdateRange = 0.4f;
}

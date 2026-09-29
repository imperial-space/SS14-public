using Robust.Shared.Map;

namespace Content.Server.Imperial.Heretic.Paths.Ash.Components;

/// <summary>
/// Еретик сейчас в форме сферы пепла (Пепельный проход): тело убрано из мира, разум в сфере.
/// </summary>
[RegisterComponent, Access(typeof(HereticAshActionsSystem))]
public sealed partial class HereticAshShiftComponent : Component
{
    /// <summary>Сфера пепла, в которую перенесён разум еретика.</summary>
    [ViewVariables]
    public EntityUid Orb;

    /// <summary>Последние проходимые клетки, по которым летела сфера. Еретик выйдет в последней.</summary>
    [ViewVariables]
    public List<EntityCoordinates> ExitPoints = new();

    [DataField]
    public int MaxExitPoints = 5;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(5);

    /// <summary>Задержка между вспышкой выхода и появлением еретика.</summary>
    [DataField]
    public TimeSpan ExitDelay = TimeSpan.FromSeconds(0.7);

    [DataField]
    public TimeSpan ExitKnockdown = TimeSpan.FromSeconds(1.3);

    /// <summary>Когда форма сферы закончится сама.</summary>
    [ViewVariables]
    public TimeSpan EndTime;

    /// <summary>Если выход уже начат — когда еретик появится в <see cref="ExitCoordinates"/>.</summary>
    [ViewVariables]
    public TimeSpan? ExitTime;

    [ViewVariables]
    public EntityCoordinates ExitCoordinates;

    /// <summary>Разум, который вернётся в тело, если в сфере его уже нет.</summary>
    [ViewVariables]
    public EntityUid ExitMind;
}

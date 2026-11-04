using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.ZLevels;

/// <summary>
/// На нижней (основной) карте стопки: файлы остальных этажей. При инициализации карты они подгружаются
/// и связываются с ней; маппер пишет их командой <c>zlevel save</c>.
/// </summary>
[RegisterComponent]
public sealed partial class ZLevelStackComponent : Component
{
    /// <summary>Этаж → файл карты этого этажа.</summary>
    [DataField]
    public Dictionary<int, ResPath> Levels = new();

    /// <summary>Этажи уже подгружены (чтобы не грузить дважды).</summary>
    [ViewVariables]
    public bool Loaded;
}

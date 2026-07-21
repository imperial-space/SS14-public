using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Forged;

[RegisterComponent]
public sealed partial class ForgedModuleComponent : Component
{
    /// <summary>
    /// В какой слот на каркасе вставляется эта часть.
    /// Например: "head", "l_arm", "r_arm", "leg".
    /// </summary>
    [DataField("partSlot"), ViewVariables(VVAccess.ReadWrite)]
    public string ModuleSlot = string.Empty;

    /// <summary>
    /// Что уже должно быть вставленно, чтобы можно было вставить этот модуль
    /// </summary>
    [DataField("requiredModule"), ViewVariables(VVAccess.ReadWrite)]
    public string RequiredModule = string.Empty;

    /// <summary>
    /// Название модуля. Скопировать с state из спрайта
    /// </summary>
    [DataField("layerState")]
    public string LayerState = string.Empty;

    /// <summary>
    /// Путь где хранятся спрайты
    /// </summary>
    [DataField("rsiPath")]
    public ResPath RsiPath = ResPath.Empty;

    /// <summary>
    /// Модификатор скорости кованного.
    /// </summary>
    [DataField("speedModifier")]
    public float SpeedModifier = 0;

    /// <summary>
    /// Модификатор резиста кованного.
    /// </summary>
    [DataField("resistanceModifier")]
    public float ResistanceModifier = 0;

    /// <summary>
    /// Будет ли эта деталь давать особые способности.
    /// </summary>
    [DataField("abilityId")]
    public string? AbilityId;
}

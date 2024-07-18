using Robust.Shared.Serialization;
using Robust.Shared.Audio;

namespace Content.Shared.Paper;

[RegisterComponent]
public sealed partial class EverythingStampComponent : Component
{
    /// <summary>
    ///    Список с собранными печатами
    /// </summary>
    [DataField("collectedStamps")]
    public List<StampDisplayInfo> CollectedStamps = [];
    
    /// <summary>
    /// Текущая выбранная печать  
    /// </summary>
    [DataField("currentStamp")]
    public string CurrentStampName = "stamp-component-stamped-name-default";
}

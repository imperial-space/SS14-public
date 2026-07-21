using Robust.Shared.Prototypes;

namespace Content.Shared.Forged;

[RegisterComponent]
public sealed partial class ForgedAssemblyComponent : Component
{
    /// <summary>
    /// Список слотов, которые доступны для вставления модуля.
    /// </summary>
    [DataField("avaibleSlots")]
    public List<string> AvaibleSlots = new() { "head", "eyes", "right_arm", "left_arm", "right_hand", "left_hand", "core", "right_leg", "left_leg" };

    /// <summary>
    /// Список слотов, что обязательно должны быть заполнены для оживления.
    /// </summary>
    [DataField("requireSlots")]
    public List<string> RequireSlots = new() { "head", "eyes", "right_arm", "left_arm", "right_hand", "left_hand", "core", "right_leg", "left_leg" };

    /// <summary>
    /// Хранит ID вставленных сущностей, чтобы потом перенести их в финального моба.
    /// </summary>
    [ViewVariables]
    public Dictionary<string, EntityUid> FittedModules = new();

    [DataField("torso"), ViewVariables(VVAccess.ReadWrite)]
    public EntProtoId? TorsoID;
}

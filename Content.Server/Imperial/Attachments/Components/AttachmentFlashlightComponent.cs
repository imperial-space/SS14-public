using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
namespace Content.Server.Imperial.Attachments.Components;

[RegisterComponent]
public sealed partial class AttachmentFlashlightComponent : Component
{
    [DataField("slotId")]
    public string SlotId { get; set; } = "gun_attachment_right";
    [ViewVariables(VVAccess.ReadWrite)]
    public bool LightOn { get; set; } = false;

    [DataField("radius")]
    public float Radius { get; set; } = 6f;

    [DataField("toggleAction", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ToggleAction = "ActionToggleAttachmentFlashlight";

    [DataField("toggleActionEntity")]
    public EntityUid? ToggleActionEntity;
}

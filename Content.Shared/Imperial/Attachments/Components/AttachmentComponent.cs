namespace Content.Shared.Imperial.Attachments.Components;

[RegisterComponent]
public sealed partial class AttachmentComponent : Component
{
    [DataField("slotId")]
    public string SlotId { get; set; } = "gun_attachment_right";
    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? Weapon { get; set; }
}

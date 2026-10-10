namespace Content.Shared.Imperial.Attachments.Components;

[RegisterComponent]
public sealed partial class WeaponAttachmentsComponent : Component
{
    public List<EntityUid> Attachments = new();
}

using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
namespace Content.Server.Imperial.Attachments.Components;

[RegisterComponent]
public sealed partial class AttachmentMufflerComponent : Component
{
    [DataField("slotId")]
    public string SlotId { get; set; } = "gun_attachment_muffler";

    [DataField("soundGunshot")]
    public SoundSpecifier SoundGunshot = new SoundPathSpecifier("/Audio/Weapons/Guns/Gunshots/silenced.ogg");
    public SoundSpecifier? SoundGunshotOriginal;
}

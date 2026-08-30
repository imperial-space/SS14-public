using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Shared.Chat.Prototypes;
using Robust.Shared.Audio;

namespace Content.Shared.Imperial.Aquila.TargetedEmote;

[Prototype]
public sealed partial class TargetedEmotePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Name = default!;

    [DataField]
    public bool Available = true;

    [DataField]
    public EmoteCategory Category = EmoteCategory.Targeted;

    [DataField]
    public SpriteSpecifier Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/Actions/scream.png"));

    /// <summary>
    /// Доступный радиус для взаимодействия
    /// </summary>
    [DataField]
    public float Range = 2f;

    /// <summary>
    /// Самое первое сообщение от того, кто предлагает взаимодействие
    /// </summary>
    [DataField]
    public string RequestMessage = string.Empty;

    /// <summary>
    /// Сообщение для того, кто предложил взаимодействие
    /// </summary>
    [DataField]
    public string ChatMessage = string.Empty;

    /// <summary>
    /// Сообщение для того, кого выбрали
    /// </summary>
    [DataField]
    public string TargetedChatMessage = string.Empty;

    /// <summary>
    /// Реквест на взаимодействие, без него ChatMessage/TargetedChatMessage проигрываются без одобрения от цели
    /// </summary>
    [DataField]
    public bool RequiresConsent = false;

    [DataField]
    public SoundSpecifier? Sound;
}

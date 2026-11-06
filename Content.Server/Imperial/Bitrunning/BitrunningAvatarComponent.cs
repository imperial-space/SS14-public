using Content.Shared.Imperial.Bitrunning;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Bitrunning;

/// <summary>/datum/component/avatar_connection: аватар и связь с телом в нетподе.</summary>
[RegisterComponent]
public sealed partial class BitrunningAvatarComponent : Component
{
    /// <summary>Зацикленный ambient_buzz домена, слышит только этот битраннер.</summary>
    [ViewVariables]
    public EntityUid? BuzzStream;

    [ViewVariables]
    public TimeSpan NextAmbience;

    [ViewVariables]
    public EntityUid Body;

    [ViewVariables]
    public EntityUid Mind;

    [ViewVariables]
    public EntityUid Server;

    [ViewVariables]
    public EntityUid Netpod;

    /// <summary>nohit: аватар не получал урона.</summary>
    [ViewVariables]
    public bool Nohit = true;

    /// <summary>Подсказка домена для действия «Информация о домене».</summary>
    [ViewVariables]
    public string? HelpText;

    [ViewVariables]
    public bool Disconnecting;

    [DataField]
    public float AvatarDamagedChance = 0.3f;
}

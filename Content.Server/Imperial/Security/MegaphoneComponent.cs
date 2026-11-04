using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Security;

// ─── Мегафон ───────────────────────────────────────────────────────────────

/// <summary>obj/item/megaphone.</summary>
[RegisterComponent]
public sealed partial class MegaphoneComponent : Component
{
    [DataField]
    public string SpeechVerb = "ImperialMegaphone";

    [DataField]
    public string EmaggedSpeechVerb = "ImperialMegaphoneEmagged";

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Imperial/Security/megaphone.ogg");

    /// <summary>spamcheck: 5 секунд.</summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(5);

    [ViewVariables]
    public TimeSpan NextUse;

    [ViewVariables]
    public bool Emagged;
}

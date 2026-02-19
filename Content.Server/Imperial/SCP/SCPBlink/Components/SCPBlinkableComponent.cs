using Content.Shared.Mobs;
using Robust.Shared.Audio;

namespace Content.Server.Imperial.SCP.SCPBlink.Components;

[RegisterComponent]
public sealed partial class SCPBlinkableComponent : Component
{
    [DataField("blinkInterval")]
    public TimeSpan BlinkInterval = TimeSpan.FromSeconds(8);

    [DataField("blinkDuration")]
    public TimeSpan BlinkDuration = TimeSpan.FromSeconds(1);

    [DataField("visualBlindDuration")]
    public TimeSpan VisualBlindDuration = TimeSpan.FromSeconds(0.15f);

    [DataField("allowCritical")]
    public bool AllowCritical = false;

    [DataField("canManualBlink")]
    public bool CanManualBlink = true;

    [DataField("manualBlinkPopup")]
    public string ManualBlinkPopup = "Вы моргнули.";

    [DataField("blinkStartPopup")]
    public string BlinkStartPopup = "Вы моргаете...";

    [DataField("blinkEndPopup")]
    public string BlinkEndPopup = "Вы снова видите четко.";

    [DataField("blinkStartSound")]
    public SoundSpecifier? BlinkStartSound = new SoundPathSpecifier("/Audio/Effects/glass_knock.ogg");

    [DataField("blinkEndSound")]
    public SoundSpecifier? BlinkEndSound = new SoundPathSpecifier("/Audio/Effects/flip.ogg");

    [DataField("manualBlinkCooldown")]
    public TimeSpan ManualBlinkCooldown = TimeSpan.FromSeconds(3);

    [ViewVariables]
    public TimeSpan NextBlinkTime;

    [ViewVariables]
    public TimeSpan BlinkEndTime;

    [ViewVariables]
    public TimeSpan NextManualBlink;

    [ViewVariables]
    public TimeSpan NextAlertUpdate;

    [ViewVariables]
    public TimeSpan VisualBlindEndTime;

    [ViewVariables]
    public bool LegacyBlindnessCleanupDone;

    [ViewVariables]
    public bool IsBlinking;
}

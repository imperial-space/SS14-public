using Content.Shared.Actions;

namespace Content.Shared.Imperial.SCP.SCPFireman.Events;

public sealed partial class SCPFiremanIgniteActionEvent : InstantActionEvent;

public sealed partial class SCPFiremanFireballActionEvent : WorldTargetActionEvent;

public sealed partial class SCPFiremanWhirlActionEvent : WorldTargetActionEvent;

public sealed partial class SCPFiremanMeltActionEvent : EntityTargetActionEvent;

public sealed partial class SCPFiremanTrueFlameActionEvent : InstantActionEvent;

public sealed partial class SCPFiremanStrikeActionEvent : InstantActionEvent;

public sealed partial class SCPFiremanSecondModeActionEvent : InstantActionEvent;

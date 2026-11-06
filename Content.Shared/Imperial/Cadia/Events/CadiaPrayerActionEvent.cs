using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Вера священника: молитва снимает напряжение псайкеров и лечит рядом.</summary>
public sealed partial class CadiaPrayerActionEvent : InstantActionEvent;

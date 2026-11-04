using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.MartialArts;

/// <summary>throw_mode: в SS14 его нет, поэтому защитная стойка включается отдельно.</summary>
public sealed partial class MartialArtsBlockActionEvent : InstantActionEvent;

using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.MartialArts;

/// <summary>implant/activate(): включить или выключить искусство из импланта.</summary>
public sealed partial class MartialArtsImplantToggleEvent : InstantActionEvent;

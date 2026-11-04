using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>dna_lock: применение привязывает костюм к ДНК носителя.</summary>
[RegisterComponent]
public sealed partial class ImperialModDnaLockComponent : Component;

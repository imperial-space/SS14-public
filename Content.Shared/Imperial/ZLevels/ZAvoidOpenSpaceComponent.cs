using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.ZLevels;

/// <summary>Моб под управлением ИИ: открытое пространство для него — стена, он обходит дыры (как NPC в SS13).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ZAvoidOpenSpaceComponent : Component;

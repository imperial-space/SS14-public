using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.ZLevels;

/// <summary>Летающее существо: не падает в проёмы между этажами.</summary>
[RegisterComponent]
public sealed partial class ZFlyerComponent : Component;

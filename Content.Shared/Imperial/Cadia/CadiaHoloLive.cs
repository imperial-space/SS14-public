using System.Numerics;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

[Serializable, NetSerializable]
public readonly record struct CadiaHoloLive(CadiaHoloLiveKind Kind, Vector2 Position, CadiaFlightLevel Level, int Squad = 0, string? Label = null, float Radius = 0f);

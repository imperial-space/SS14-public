using Content.Shared.DoAfter;
using Content.Shared.Projectiles;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Большой момент войны: вспышка и тряска у всех.</summary>
[Serializable, NetSerializable]
public sealed class CadiaScreenFxEvent(CadiaScreenFx kind) : EntityEventArgs
{
    public readonly CadiaScreenFx Kind = kind;
}

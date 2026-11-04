using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Cadia;

[Serializable, NetSerializable]
public enum CadiaVoxCommand : byte
{
    /// <summary>«Огонь на подавление!» — точнее стрельба.</summary>
    Suppress,

    /// <summary>«Стоять насмерть!» — меньше урона.</summary>
    Hold,

    /// <summary>«Вперёд, за Императора!» — быстрее бег.</summary>
    Charge,
}

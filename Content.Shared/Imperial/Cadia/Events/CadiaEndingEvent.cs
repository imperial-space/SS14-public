using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Показать финальную сцену.</summary>
[Serializable, NetSerializable]
public sealed class CadiaEndingEvent(CadiaEnding ending, int saved) : EntityEventArgs
{
    public readonly CadiaEnding Ending = ending;
    public readonly int Saved = saved;
}

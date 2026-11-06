using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Cult;

[Serializable, NetSerializable]
public sealed class CultChoiceState(string title, List<CultMenuOption> options, bool radial) : BoundUserInterfaceState
{
    public readonly string Title = title;
    public readonly List<CultMenuOption> Options = options;

    /// <summary>Радиальное меню (иначе список кнопок).</summary>
    public readonly bool Radial = radial;
}

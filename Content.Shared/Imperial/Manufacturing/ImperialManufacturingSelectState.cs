using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Manufacturing;

/// <summary>tgui_input_list: выбор чертежа, рецепта, фильтра.</summary>
[Serializable, NetSerializable]
public sealed class ImperialManufacturingSelectState(string title, string purpose, List<ImperialManufacturingSelectOption> options) : BoundUserInterfaceState
{
    public readonly string Title = title;
    public readonly string Purpose = purpose;
    public readonly List<ImperialManufacturingSelectOption> Options = options;
}

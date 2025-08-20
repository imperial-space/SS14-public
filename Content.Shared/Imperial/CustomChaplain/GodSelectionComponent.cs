using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.CustomChaplain
{
    /// <summary>
    /// Component that allows a chaplain to choose their deity once
    /// </summary>
    [RegisterComponent, NetworkedComponent]
    public sealed partial class GodSelectionComponent : Component
    {
        /// <summary>
        /// Whether the god has already been selected
        /// </summary>
        [DataField]
        public bool GodSelected;

        /// <summary>
        /// The selected god's name
        /// </summary>
        [DataField]
        public string? SelectedGod;

        /// <summary>
        /// Whether this is a custom god
        /// </summary>
        [DataField]
        public bool IsCustomGod;
    }

    [Serializable, NetSerializable]
    public enum GodSelectionUiKey
    {
        Key,
    }

    [Serializable, NetSerializable]
    public sealed class GodSelectionBuiState(bool godSelected, string? selectedGod, bool isCustomGod)
        : BoundUserInterfaceState
    {
        public readonly bool GodSelected = godSelected;
        public readonly string? SelectedGod = selectedGod;
        public readonly bool IsCustomGod = isCustomGod;
    }

    [Serializable, NetSerializable]
    public sealed class GodSelectionChooseGodMessage(string godName, bool isCustom) : BoundUserInterfaceMessage
    {
        public readonly string GodName = godName;
        public readonly bool IsCustom = isCustom;
    }
}

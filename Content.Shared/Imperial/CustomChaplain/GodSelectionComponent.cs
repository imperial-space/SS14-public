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
        [DataField("godSelected")]
        public bool GodSelected = false;

        /// <summary>
        /// The selected god's name
        /// </summary>
        [DataField("selectedGod")]
        public string? SelectedGod = null;

        /// <summary>
        /// Whether this is a custom god
        /// </summary>
        [DataField("isCustomGod")]
        public bool IsCustomGod = false;
    }

    [Serializable, NetSerializable]
    public enum GodSelectionUiKey
    {
        Key
    }

    [Serializable, NetSerializable]
    public sealed class GodSelectionBuiState : BoundUserInterfaceState
    {
        public readonly bool GodSelected;
        public readonly string? SelectedGod;
        public readonly bool IsCustomGod;

        public GodSelectionBuiState(bool godSelected, string? selectedGod, bool isCustomGod)
        {
            GodSelected = godSelected;
            SelectedGod = selectedGod;
            IsCustomGod = isCustomGod;
        }
    }

    [Serializable, NetSerializable]
    public sealed class GodSelectionChooseGodMessage : BoundUserInterfaceMessage
    {
        public readonly string GodName;
        public readonly bool IsCustom;

        public GodSelectionChooseGodMessage(string godName, bool isCustom)
        {
            GodName = godName;
            IsCustom = isCustom;
        }
    }
}

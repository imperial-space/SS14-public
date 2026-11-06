using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.BluespaceTap;

/// <summary>Состояние корпуса блюспейс-сборщика (update_icon_state).</summary>
[Serializable, NetSerializable]
public enum BluespaceTapVisuals : byte
{
    State,
    Screen,
}

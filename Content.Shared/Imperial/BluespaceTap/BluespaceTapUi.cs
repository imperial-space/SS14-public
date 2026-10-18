using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.BluespaceTap;

[Serializable, NetSerializable]
public enum BluespaceTapUiKey : byte
{
    Key,
}

/// <summary>Состояние корпуса блюспейс-сборщика (update_icon_state).</summary>
[Serializable, NetSerializable]
public enum BluespaceTapVisuals : byte
{
    State,
    Screen,
}

[Serializable, NetSerializable]
public enum BluespaceTapVisualState : byte
{
    Off,
    Level0,
    Level1,
    Level2,
    Level3,
    Level4,
    Level5,
    Cascade,
}

[Serializable, NetSerializable]
public enum BluespaceTapScreenState : byte
{
    None,
    Screen,
    Dirty,
    Cascade,
}

[Serializable, NetSerializable]
public sealed class BluespaceTapProductState(int key, string name, int price)
{
    public readonly int Key = key;
    public readonly string Name = name;
    public readonly int Price = price;
}

/// <summary>ui_data из bluespace_tap.dm.</summary>
[Serializable, NetSerializable]
public sealed class BluespaceTapUiState : BoundUserInterfaceState
{
    public float DesiredMiningPower;
    public float MiningPower;
    public float Points;
    public float TotalPoints;
    public float PowerUse;
    public float AvailablePower;
    public bool Emagged;
    public bool Dirty;
    public bool AutoShutdown;
    public bool Stabilizers;
    public float StabilizerPower;
    public bool StabilizerPriority;
    public bool Portaling;
    public List<BluespaceTapProductState> Products = new();
}

[Serializable, NetSerializable]
public sealed class BluespaceTapSetPowerMessage(float power) : BoundUserInterfaceMessage
{
    public readonly float Power = power;
}

[Serializable, NetSerializable]
public sealed class BluespaceTapVendMessage(int key) : BoundUserInterfaceMessage
{
    public readonly int Key = key;
}

[Serializable, NetSerializable]
public sealed class BluespaceTapToggleAutoShutdownMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class BluespaceTapToggleStabilizersMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class BluespaceTapToggleStabilizerPriorityMessage : BoundUserInterfaceMessage;

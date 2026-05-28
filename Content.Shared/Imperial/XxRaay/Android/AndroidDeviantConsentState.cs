using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.XxRaay.Android;

/// <summary>
/// Ключ UI окна согласия на передачу девиантности
/// </summary>
[Serializable, NetSerializable]
public enum AndroidDeviantConsentUiKey : byte
{
    Key
}

/// <summary>
/// Состояние UI согласия на передачу девиантности
/// </summary>
[Serializable, NetSerializable]
public sealed class AndroidDeviantConsentBuiState : BoundUserInterfaceState
{
    public readonly string ConverterName;

    public AndroidDeviantConsentBuiState(string converterName)
    {
        ConverterName = converterName;
    }
}


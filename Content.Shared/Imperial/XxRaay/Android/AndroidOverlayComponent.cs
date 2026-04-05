using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.XxRaay.Android;

/// <summary>
/// Включает клиентский оверлей андроидов для владельца компонента
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AndroidOverlayComponent : Component
{
    [DataField]
    public string IconSprite = "Imperial/XxRaay/android.rsi/android-overlay";

    [DataField]
    public float IconScale = 0.35f;
}


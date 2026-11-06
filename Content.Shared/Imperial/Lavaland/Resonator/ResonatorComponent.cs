using Robust.Shared.GameObjects;

namespace Content.Shared.Imperial.Lavaland.Resonator;

[RegisterComponent]
public sealed partial class ResonatorComponent : Component
{
    [DataField]
    public ResonatorMode Mode = ResonatorMode.Auto;

    [DataField]
    public int FieldLimit = 4;

    [DataField]
    public float QuickBurstMod = 0.8f;

    [DataField]
    public float AddingFailure = 50f;

    [DataField]
    public bool CanMatrix = false;

    [ViewVariables]
    public List<EntityUid> Fields = new();

    /// <summary>Жизнь поля в автоматическом режиме.</summary>
    [DataField]
    public TimeSpan AutoFieldDuration = TimeSpan.FromSeconds(2);

    /// <summary>Жизнь поля в ручном режиме и в режиме матрицы.</summary>
    [DataField]
    public TimeSpan ManualFieldDuration = TimeSpan.FromSeconds(60);

    [DataField]
    public float AfterInteractRange = 0.6f;
}

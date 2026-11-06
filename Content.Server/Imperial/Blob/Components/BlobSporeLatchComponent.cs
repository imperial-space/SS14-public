using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Blob.Components;

/// <summary>Спора, нацелившаяся на заражение.</summary>
[RegisterComponent]
public sealed partial class BlobSporeLatchComponent : Component
{
    /// <summary>Дистанция, с которой спора начинает забираться на голову.</summary>
    [DataField] public float InfectionRange = 1.2f;

    /// <summary>Насколько близко спора подлетает к жертве (меньше InfectionRange).</summary>
    [DataField] public float ApproachRange = 0.6f;

    /// <summary>Радиус поиска жертв в критическом состоянии.</summary>
    [DataField] public float TargetSearchRange = 20f;

    /// <summary>Время, за которое спора забирается на голову.</summary>
    [DataField] public TimeSpan LatchDuration = TimeSpan.FromSeconds(5f);

    [ViewVariables] public EntityUid? LatchTarget;
    [ViewVariables] public bool LatchInProgress;
    [ViewVariables] public TimeSpan NextThink;
}

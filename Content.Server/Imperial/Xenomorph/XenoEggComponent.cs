using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenomorph;

/// <summary>Яйцо: растёт 90–150 с, лопается при приближении носителя.</summary>
[RegisterComponent]
public sealed partial class XenoEggComponent : Component
{
    [DataField]
    public TimeSpan MinGrowth = TimeSpan.FromSeconds(90);

    [DataField]
    public TimeSpan MaxGrowth = TimeSpan.FromSeconds(150);

    [DataField]
    public EntProtoId Facehugger = "ImperialXenoFacehugger";

    [DataField]
    public float TriggerRange = 1.5f;

    [DataField]
    public bool StartGrown;

    [ViewVariables] public Shared.Imperial.Xenomorph.XenoEggState State;
    [ViewVariables] public TimeSpan NextStateAt;

    /// <summary>Burst(kill): разбитое яйцо выпускает мёртвого лицехвата.</summary>
    [ViewVariables] public bool KillChild;
}

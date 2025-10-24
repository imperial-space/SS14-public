using Robust.Shared.Timing;
using Content.Server.Imperial.Halloween;

namespace Content.Server.Imperial.Halloween.Components;
/// <summary>
/// Техничесский компонент тыквы для завершения раунда в HalloweenTechnicalRule
/// </summary>
[RegisterComponent]
[Access(typeof(JackPumpkinSystem))]
public sealed partial class JackPumpkinComponent : Component
{
    [DataField]
    public bool EventRaised = false;

    [DataField]
    public bool GetDelay = false;

    /// <summary>
    /// Через какое время после спавна раунд должен завершиться, если HalloweenTechnical геймрул активен.
    /// </summary>
    [DataField("endRoundDelay")]
    public TimeSpan Delay = TimeSpan.Zero;
}

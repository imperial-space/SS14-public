using Content.Shared.Destructible.Thresholds;

namespace Content.Server.Imperial.Medieval.Plague;

[RegisterComponent]
public sealed partial class MedievalPlagueClumsinessComponent : Component
{
    [DataField]
    public MinMax Delay;

    public TimeSpan NextFall = TimeSpan.Zero;
}

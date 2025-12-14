using Content.Server.EUI;

namespace Content.Server.Imperial.Medieval.Afk;

public sealed class MedievalAfkEui : BaseEui
{
    public MedievalAfkEui()
    {
        IoCManager.InjectDependencies(this);
    }
}

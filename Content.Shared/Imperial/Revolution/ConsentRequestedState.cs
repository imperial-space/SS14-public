using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Revolutionary;

[Serializable, NetSerializable]
public sealed class ConsentRequestedState (string converterName) : EuiStateBase
{
    public string ConverterName = converterName;
    public bool Close = false;
}

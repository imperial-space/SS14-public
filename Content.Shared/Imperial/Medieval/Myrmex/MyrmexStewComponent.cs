using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Myrmex
{
    [RegisterComponent]
    public sealed partial class MyrmexStewComponent : Component
    {
        [DataField] public int Uses = 3;
        [DataField] public MyrmexBuff? Buff;
        [DataField] public bool EdibleByLarva = true;
    }
}

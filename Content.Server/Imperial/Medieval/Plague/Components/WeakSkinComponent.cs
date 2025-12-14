using Content.Shared.Damage;

namespace Content.Server.Imperial.Medieval.Plague;

[RegisterComponent]
public sealed partial class WeakSkinComponent : Component
{
    [DataField]
    public Dictionary<string, float> TypeModifiers = new();
}

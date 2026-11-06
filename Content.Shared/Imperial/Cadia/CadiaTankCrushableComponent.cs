using Content.Shared.DoAfter;
using Content.Shared.Projectiles;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Танк давит это гусеницами: колючая проволока, колья.</summary>
[RegisterComponent]
public sealed partial class CadiaTankCrushableComponent : Component
{
    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Effects/metal_break1.ogg");
}

using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Оружие «греется»: плазма иногда обжигает стрелка, как ей и положено.</summary>
[RegisterComponent]
public sealed partial class CadiaGetsHotComponent : Component
{
    [DataField]
    public float Chance = 0.03f;

    [DataField(required: true)]
    public DamageSpecifier Burn = new();

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Effects/lightburn.ogg");
}

using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

// ═══════════════════════ Пустотный щит ═══════════════════════

/// <summary>
/// Пустотный щит корабля: поглощает орбитальные удары и попадания ПВО по своей сетке, пока не иссякнет,
/// затем гаснет на время перезапуска.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaVoidShieldComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Strength = 100f;

    [DataField, AutoNetworkedField]
    public float MaxStrength = 100f;

    /// <summary>Восстановление в секунду, пока щит поднят.</summary>
    [DataField]
    public float Regen = 1.5f;

    [DataField]
    public TimeSpan RebootTime = TimeSpan.FromSeconds(60);

    [DataField, AutoNetworkedField]
    public TimeSpan? RebootAt;

    [DataField]
    public SoundSpecifier AbsorbSound = new SoundPathSpecifier("/Audio/Imperial/MiningWeapons/Smasher/activate_shield.ogg");

    [DataField]
    public SoundSpecifier CollapseSound = new SoundPathSpecifier("/Audio/Imperial/ModularShield/empulse.ogg");

    [DataField]
    public SoundSpecifier RebootSound = new SoundPathSpecifier("/Audio/Imperial/MiningWeapons/Smasher/start_charging_shield.ogg");

    /// <summary>
    /// Радиус купола в клетках. 0 — щит корабля: прикрывает всю свою сетку. У щита на земле сетка — всё поле,
    /// поэтому он прикрывает только круг вокруг себя.
    /// </summary>
    [DataField]
    public float Radius;
}

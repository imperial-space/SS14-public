using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Lavaland.Colossus;

/// <summary>
/// Колосс (megafauna/colossus из SS13): спираль, случайный залп, дробовик, крест/диагонали
/// и одноразовый финал на 10% здоровья.
/// </summary>
[RegisterComponent]
public sealed partial class ColossusComponent : Component
{
    [DataField] public EntProtoId Bolt = "ImperialColossusBolt";

    /// <summary>speed 0.5 клетки за тик SSprojectiles (20 в секунду).</summary>
    [DataField] public float BoltSpeed = 10f;

    /// <summary>SLEEP_CHECK_DEATH перед любым залпом.</summary>
    [DataField] public float WindUp = 1.5f;

    [DataField] public float SpiralCooldown = 1.5f;
    [DataField] public float RandomCooldown = 1.5f;
    [DataField] public float ShotgunCooldown = 0.5f;
    [DataField] public float DirShotsCooldown = 2.5f;
    [DataField] public float FinalCooldown = 2.5f;
    [DataField] public float[] ShotgunAngles = { 12.5f, 7.5f, 2.5f, -2.5f, -7.5f, -12.5f };

    [DataField] public SoundSpecifier ShotSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_magic_clockwork_invoke_general.ogg");
    [DataField] public SoundSpecifier TelegraphSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_magic_narsie_attack.ogg");
    [DataField] public EntProtoId Shield = "ImperialColossusShield";

    [ViewVariables] public bool FinalAvailable = true;
}

/// <summary>Смертельный болт колосса: превращает трупы в пепел и разрушает породу.</summary>
[RegisterComponent]
public sealed partial class ColossusBoltComponent : Component;

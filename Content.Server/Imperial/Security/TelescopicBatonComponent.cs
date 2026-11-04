using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Security;

// ─── Телескопическая дубинка ───────────────────────────────────────────────

/// <summary>melee/baton/telescopic.</summary>
[RegisterComponent]
public sealed partial class TelescopicBatonComponent : Component
{
    /// <summary>cooldown.</summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(4);

    /// <summary>knockdown_time.</summary>
    [DataField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(1.5);

    /// <summary>stamina_damage.</summary>
    [DataField]
    public float StaminaDamage = 55f;

    /// <summary>clumsy_knockdown_time.</summary>
    [DataField]
    public TimeSpan ClumsyKnockdownTime = TimeSpan.FromSeconds(15);

    /// <summary>dazed: на сколько противника ошеломляет.</summary>
    [DataField]
    public TimeSpan DazeTime = TimeSpan.FromSeconds(3);

    [DataField]
    public SoundSpecifier StunSound = new SoundPathSpecifier("/Audio/Imperial/MartialArts/woodhit.ogg");

    [ViewVariables]
    public TimeSpan NextStun;
}

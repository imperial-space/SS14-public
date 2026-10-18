using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Items;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticGlitteringArrayComponent : Component
{
    [DataField]
    public EntityUid? Wearer;

    /// <summary>damage_modifier: весь полученный урон уходит в мозг с этим множителем.</summary>
    [DataField]
    public float DamageModifier = 1.15f;

    /// <summary>process(): не еретику роба сама выжигает мозг.</summary>
    [DataField]
    public float NonHereticBrainDamage = 20f;

    /// <summary>Интервал process() в SSobj.</summary>
    [DataField]
    public TimeSpan TickInterval = TimeSpan.FromSeconds(2);

    [ViewVariables]
    public TimeSpan NextTick;

    /// <summary>braindead: отсчёт до смерти уже запущен.</summary>
    [ViewVariables]
    public bool Braindead;

    /// <summary>addtimer(kill_wearer, 5 SECONDS).</summary>
    [ViewVariables]
    public TimeSpan DeathAt;
}

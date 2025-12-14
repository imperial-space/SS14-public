using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Imperial.Medieval.GameTicking.Rules;

[RegisterComponent]
public sealed partial class RoundStatCounterRuleComponent : Component
{
    [DataField]
    public int TotalPotions = 0;

    [DataField]
    public int TotalLockpicks = 0;

    [DataField]
    public int TotalCrafts = 0;

    [DataField]
    public int TotalDiggs = 0;

    [DataField]
    public int GhostBoo = 0;

    [DataField]
    public int GhostBooPlayers = 0;

    [DataField]
    public int AlcoholDrink = 0;

    [DataField]
    public int SpikeTrapActiveted = 0;

    [DataField]
    public int HumanHurt = 0;

    [DataField]
    public int Screams = 0;

    [DataField]
    public float ZveresHeat = 0;

    [DataField]
    public int HumanDeath = 0;

    [DataField]
    public string FirstDeath = "nobody";

    [DataField]
    public int OpenedDungeons = 0;

    [DataField]
    public string FirstDungeonVisiter = "nobody";
}

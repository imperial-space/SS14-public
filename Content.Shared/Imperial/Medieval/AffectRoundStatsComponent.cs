namespace Content.Shared.Imperial.Medieval.GameTicking.Rules;


[RegisterComponent]
public sealed partial class AffectRoundStatsComponent : Component
{
    [DataField]
    public int HitCount = 0;

    [DataField]
    public int Screams = 0;

    [DataField]
    public int Potions = 0;

    [DataField]
    public int Lockpicks = 0;

    [DataField]
    public int Crafts = 0;

    [DataField]
    public int Diggs = 0;

    [DataField]
    public int Alcohol = 0;
}

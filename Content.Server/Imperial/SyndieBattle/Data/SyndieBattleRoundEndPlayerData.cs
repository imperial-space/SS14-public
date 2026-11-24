namespace Content.Server.Imperial.SyndieBattle.Data;

[DataDefinition]
public partial record struct SyndieBattleRoundEndPlayerData
{
    [DataField]
    public string Name = string.Empty;

    [DataField]
    public int Kills;

    [DataField]
    public int Deaths;

    [DataField]
    public int TC;

    [DataField]
    public int SpentTc;

    [DataField]
    public int StolenTc;

    [DataField]
    public TimeSpan Duration;

    [DataField]
    public bool Alive;

    [DataField]
    public int ArenaAttempts;

    [DataField]
    public int ArenaCompletions;

    [DataField]
    public int RedeemedPdas;

    [DataField]
    public int RedeemedTc;

    [DataField]
    public int BestStreak;

    [DataField]
    public int ComebackTc;
}

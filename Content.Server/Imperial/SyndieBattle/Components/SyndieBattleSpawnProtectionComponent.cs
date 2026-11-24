namespace Content.Server.Imperial.SyndieBattle.Components;

[RegisterComponent]
public sealed partial class SyndieBattleSpawnProtectionComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan EndTime;

    [ViewVariables(VVAccess.ReadOnly)]
    public LocId EndMessage = "syndiebattle-spawn-protection-end";

    [ViewVariables(VVAccess.ReadOnly)]
    public bool AddedGodmode;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool AddedPacified;
}

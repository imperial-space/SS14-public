namespace Content.Server.Imperial.Medieval.Lycantropy;

[DataDefinition]
public sealed partial class SetWerewolfMobThresholdsEvent : EntityEventArgs
{
    [DataField]
    public float Crit;

    [DataField]
    public float Death;
}

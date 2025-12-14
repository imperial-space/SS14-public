namespace Content.Server.Imperial.Medieval.Lycantropy;

[DataDefinition]
public sealed partial class SetWerewolfDashStrengthEvent : EntityEventArgs
{
    [DataField]
    public float Modifier;
}

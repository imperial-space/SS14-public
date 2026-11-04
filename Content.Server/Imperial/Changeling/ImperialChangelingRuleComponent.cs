namespace Content.Server.Imperial.Changeling;

/// <summary>Правило генокрадов TG: кого сделали генокрадами.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingRuleComponent : Component
{
    [ViewVariables]
    public List<EntityUid> Minds = new();
}

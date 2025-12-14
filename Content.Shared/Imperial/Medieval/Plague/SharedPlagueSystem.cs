namespace Content.Shared.Imperial.Medieval.Plague;

public abstract partial class SharedMedievalPlagueSystem : EntitySystem
{
    public abstract void TryProgressInfection(EntityUid uid, float amount, string? reagent);
    public abstract void GrantPlagueImmunity(EntityUid uid, string? cure);
}

using Content.Shared.Humanoid;

namespace Content.Shared.IdentityManagement.Components;

public interface IIdentityInterface
{
    public string GetIdentityName(EntityUid target, IdentityComponent comp, IdentityRepresentation representation, EntityUid? examiner);
    public IdentityRepresentation GetIdentityRepresentation(EntityUid target, HumanoidAppearanceComponent? appearance = null);
}

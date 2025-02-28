using Content.Shared.Chat;
using Content.Server.Speech.Components;
using Content.Shared.IdentityManagement.Components; // Imperial Spellward Identity
using Content.Server.IdentityManagement; // Imperial Spellward Identity

namespace Content.Server.Speech.EntitySystems;

public sealed partial class VoiceOverrideSystem : EntitySystem
{
    [Dependency] private readonly IdentitySystem _identity = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VoiceOverrideComponent, TransformSpeakerNameEvent>(OnTransformSpeakerName);
    }

    private void OnTransformSpeakerName(Entity<VoiceOverrideComponent> entity, ref TransformSpeakerNameEvent args)
    {
        if (!entity.Comp.Enabled)
            return;

        args.VoiceName = _identity.GetIdentityName(entity.Owner, EnsureComp<IdentityComponent>(entity.Owner), _identity.GetIdentityRepresentation(entity.Owner, null), null);
        args.SpeechVerb = entity.Comp.SpeechVerbOverride ?? args.SpeechVerb;
    }
}

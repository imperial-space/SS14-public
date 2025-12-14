using Content.Shared.Imperial.Medieval.MagicRunes.Components;
using Content.Shared.Interaction.Events;

//=========================================================================
// MagicRuneSystem.Core.cs
//=========================================================================
// Purpose: Core event handlers for magic rune system initialization
// Author: rhailrake
//=========================================================================

namespace Content.Shared.Imperial.Medieval.MagicRunes.Systems;

public partial class MagicRuneSystem
{
    public void InitializeCore()
    {
        SubscribeLocalEvent<MagicRuneKnowledgeComponent, MapInitEvent>(OnKnowledgeInit);

        SubscribeLocalEvent<MagicScrollComponent, MapInitEvent>(OnScrollInit);

        SubscribeLocalEvent<MagicStoneComponent, UseInHandEvent>(OnMagicStoneActivatedInHands);
    }

    private void OnKnowledgeInit(EntityUid uid, MagicRuneKnowledgeComponent component, MapInitEvent args)
    {
        var intelligence = GetIntelligence(uid);
        PopulateStartRunes(uid, component, intelligence);
    }

    private void OnScrollInit(EntityUid uid, MagicScrollComponent component, MapInitEvent args)
    {
        InitializeScroll(uid, component);
    }

    private void OnMagicStoneActivatedInHands(EntityUid uid, MagicStoneComponent component, UseInHandEvent args)
    {
        HandleRuneLearning(args.User, uid, component.Rune);
    }
}

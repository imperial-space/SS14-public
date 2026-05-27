using Content.Shared.Imperial.PossessedBlade;
using Content.Shared.Mind.Components;
using Robust.Server.GameObjects;

namespace Content.Server.Imperial.PossessedBlade;

/// <summary>
/// Handles the visual feedback (point light) for the possessed blade
/// when a ghost inhabits or leaves it.
/// The ghost role search / wipe logic is handled by ToggleableGhostRoleSystem.
/// </summary>
public sealed class PossessedBladeSystem : EntitySystem
{
    [Dependency] private readonly PointLightSystem _light = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PossessedBladeComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<PossessedBladeComponent, MindRemovedMessage>(OnMindRemoved);
    }

    /// <summary>
    /// Ghost inhabited the blade — enable the purple glow.
    /// </summary>
    private void OnMindAdded(EntityUid uid, PossessedBladeComponent component, MindAddedMessage args)
    {
        _light.SetEnabled(uid, true);
    }

    /// <summary>
    /// Ghost left the blade — extinguish the glow.
    /// </summary>
    private void OnMindRemoved(EntityUid uid, PossessedBladeComponent component, MindRemovedMessage args)
    {
        _light.SetEnabled(uid, false);
    }
}

using Content.Server.Chat.Systems;
using Content.Shared.ActionBlocker;
using Content.Server.Imperial.Cult.Components;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Imperial.Cult.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult;

public sealed partial class CultSystem
{
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;

    private static readonly ProtoId<EmotePrototype> ScreamEmote = "Scream";

    private void InitializeBody()
    {
        SubscribeLocalEvent<CultImmobilizedComponent, UpdateCanMoveEvent>(OnImmobilizedMove);
    }

    private void OnImmobilizedMove(Entity<CultImmobilizedComponent> ent, ref UpdateCanMoveEvent args)
    {
        args.Cancel();
    }

    /// <summary>TRAIT_IMMOBILIZED.</summary>
    public void Immobilize(EntityUid uid, bool enabled)
    {
        if (enabled)
            EnsureComp<CultImmobilizedComponent>(uid);
        else
            RemComp<CultImmobilizedComponent>(uid);
        _blocker.UpdateCanMove(uid);
    }

    /// <summary>emote("scream").</summary>
    public void Scream(EntityUid uid)
    {
        _chat.TryEmoteWithChat(uid, ScreamEmote, ignoreActionBlocker: true);
    }

    /// <summary>obj/effect/blessing (освящённый пол). В SS14 нет — оставлено для капеллана.</summary>
    public bool IsBlessed(EntityCoordinates coords)
    {
        foreach (var _ in _lookup.GetEntitiesInRange<CultBlessingComponent>(coords, 0.45f))
            return true;
        return false;
    }
}

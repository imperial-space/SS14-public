using Content.Shared.Popups;
using Content.Shared.Storage.Components;

namespace Content.Shared.Imperial.Uplink.AssaultPod;

/// <summary>Двери капсулы заперты с начала отсчёта и до посадки.</summary>
public sealed class SharedAssaultPodSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AssaultPodComponent, StorageOpenAttemptEvent>(OnOpenAttempt);
    }

    private void OnOpenAttempt(Entity<AssaultPodComponent> ent, ref StorageOpenAttemptEvent args)
    {
        if (ent.Comp.State is not (AssaultPodState.Launching or AssaultPodState.Flying))
            return;

        args.Cancelled = true;
        if (!args.Silent)
            _popup.PopupClient(Loc.GetString("assault-pod-locked"), ent, args.User);
    }
}

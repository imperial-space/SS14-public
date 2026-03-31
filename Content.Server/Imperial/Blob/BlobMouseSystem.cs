using Content.Server.Actions;
using Content.Server.GameTicking.Rules;
using Content.Server.Ghost.Roles.Events;
using Content.Server.Imperial.Blob.Components;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Shared.Imperial.Blob;
using Content.Shared.Mobs;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.Blob;

public sealed class BlobMouseSystem : EntitySystem
{
    private const string BlobMutateSound = "/Audio/Imperial/blob/sound_magic_mutate.ogg";

    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly BlobMobSystem _blobMob = default!;
    [Dependency] private readonly BlobRuleSystem _blobRule = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobMouseComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BlobMouseComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BlobMouseComponent, BlobMouseTransformActionEvent>(OnTransformAction);
        SubscribeLocalEvent<BlobMouseComponent, GhostRoleSpawnerUsedEvent>(OnGhostRoleSpawnerUsed);
        SubscribeLocalEvent<BlobMouseComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BlobMouseComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Triggered)
                continue;

            comp.TransformAccumulator += frameTime;
            if (comp.TransformAccumulator < comp.TransformDelay)
                continue;

            TryTransformIntoBlob(uid, comp, true);
        }
    }

    private void OnStartup(EntityUid uid, BlobMouseComponent comp, ComponentStartup args)
    {
        comp.TransformAction = _actions.AddAction(uid, comp.TransformActionPrototype);
        _blobMob.ConfigureBlobFriendlyCollision(uid);
        Dirty(uid, comp);
    }

    private void OnShutdown(EntityUid uid, BlobMouseComponent comp, ComponentShutdown args)
    {
        _blobMob.RestoreBlobFriendlyCollision(uid);

        if (comp.TransformAction is { } action && Exists(action))
            _actions.RemoveAction(uid, action);
    }

    private void OnTransformAction(EntityUid uid, BlobMouseComponent comp, BlobMouseTransformActionEvent args)
    {
        args.Handled = true;
        TryTransformIntoBlob(uid, comp, false);
    }

    private void OnGhostRoleSpawnerUsed(EntityUid uid, BlobMouseComponent comp, GhostRoleSpawnerUsedEvent args)
    {
        if (!TryComp<BlobRuleSourceComponent>(args.Spawner, out var source))
            return;

        comp.SourceRule = source.Rule;
        comp.Chemical = source.Chemical;
        Dirty(uid, comp);
    }

    private void OnMobStateChanged(EntityUid uid, BlobMouseComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || comp.Triggered)
            return;

        TryTransformIntoBlob(uid, comp, false);
    }

    private void TryTransformIntoBlob(EntityUid uid, BlobMouseComponent comp, bool timed)
    {
        if (comp.Triggered)
            return;

        if (!_mind.TryGetMind(uid, out var mindId, out var mind))
            return;

        comp.Triggered = true;
        Dirty(uid, comp);

        _audio.PlayPvs(BlobMutateSound, uid);

        _blobRule.TryStartBlob(uid, mindId, mind, comp.Chemical, deleteSource: true, announce: true, ruleOverride: comp.SourceRule);

        if (timed && Exists(uid))
            _popup.PopupEntity(Loc.GetString("blob-mouse-awakens"), uid, uid, PopupType.Medium);
    }
}
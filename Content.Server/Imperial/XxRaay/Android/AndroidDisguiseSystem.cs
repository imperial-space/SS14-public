using System;
using Content.Shared.CCVar;
using Content.Shared.Imperial.XxRaay.Android;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.XxRaay.Android;

/// <summary>
/// Серверная система маскировки андроида
/// </summary>
public sealed class AndroidDisguiseSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AndroidDisguiseComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AndroidDisguiseComponent, AndroidToggleDisguiseEvent>(OnToggleDisguise);
        SubscribeLocalEvent<AndroidDisguiseComponent, AndroidDisguiseNameChosenMessage>(OnNameChosen);
        SubscribeLocalEvent<AndroidDisguiseComponent, MindAddedMessage>(OnMindAdded);
    }

    private void OnMapInit(Entity<AndroidDisguiseComponent> ent, ref MapInitEvent args)
    {
        ref var comp = ref ent.Comp;

        comp.State = AndroidDisguiseState.Android;
        comp.NextStateTime = TimeSpan.Zero;
        comp.HumanName = null;
        comp.OriginalName = null;

        Dirty(ent, comp);
    }

    private void OnToggleDisguise(Entity<AndroidDisguiseComponent> ent, ref AndroidToggleDisguiseEvent args)
    {
        if (args.Handled)
            return;

        ref var comp = ref ent.Comp;
        var uid = (EntityUid) ent;

        switch (comp.State)
        {
            case AndroidDisguiseState.Android:
                StartTransformToHuman(uid, ref comp);
                break;

            case AndroidDisguiseState.Human:
                StartTransformToAndroid(uid, ref comp);
                break;

            default:
                return;
        }

        args.Handled = true;
    }

    private void OnMindAdded(EntityUid uid, AndroidDisguiseComponent comp, MindAddedMessage args)
    {
        if (comp.HumanName != null)
            return;

        _ui.TryOpenUi(uid, AndroidDisguiseNameUiKey.Key, uid);
        _ui.SetUiState(uid, AndroidDisguiseNameUiKey.Key, new AndroidDisguiseNameBuiState());
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AndroidDisguiseComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.State is AndroidDisguiseState.TransformingToHuman or AndroidDisguiseState.TransformingToAndroid &&
                now >= comp.NextStateTime)
            {
                FinishTransition(uid, ref comp);
            }
        }
    }

    private void StartTransformToHuman(EntityUid uid, ref AndroidDisguiseComponent comp)
    {
        comp.State = AndroidDisguiseState.TransformingToHuman;
        comp.NextStateTime = _timing.CurTime + comp.TransformDuration;
        Dirty(uid, comp);
    }

    private void StartTransformToAndroid(EntityUid uid, ref AndroidDisguiseComponent comp)
    {
        comp.State = AndroidDisguiseState.TransformingToAndroid;
        comp.NextStateTime = _timing.CurTime + comp.RetransformDuration;
        Dirty(uid, comp);
    }

    private void FinishTransition(EntityUid uid, ref AndroidDisguiseComponent comp)
    {
        switch (comp.State)
        {
            case AndroidDisguiseState.TransformingToHuman:
                comp.State = AndroidDisguiseState.Human;
                ApplyHumanName(uid, ref comp);
                break;

            case AndroidDisguiseState.TransformingToAndroid:
                comp.State = AndroidDisguiseState.Android;
                RestoreOriginalName(uid, ref comp);
                break;
        }

        Dirty(uid, comp);
    }

    private void OnNameChosen(EntityUid uid, AndroidDisguiseComponent comp, AndroidDisguiseNameChosenMessage msg)
    {
        var name = msg.Name.Trim();
        if (name.Length == 0)
            return;

        var maxNameLength = _cfg.GetCVar(CCVars.MaxNameLength);
        if (name.Length > maxNameLength)
            name = name[..maxNameLength];

        comp.HumanName = name;
        Dirty(uid, comp);
    }

    private void ApplyHumanName(EntityUid uid, ref AndroidDisguiseComponent comp)
    {
        if (string.IsNullOrEmpty(comp.HumanName))
            return;

        if (comp.OriginalName == null)
        {
            var meta = MetaData(uid);
            comp.OriginalName = meta.EntityName;
        }

        _metaData.SetEntityName(uid, comp.HumanName);
    }

    private void RestoreOriginalName(EntityUid uid, ref AndroidDisguiseComponent comp)
    {
        if (string.IsNullOrEmpty(comp.OriginalName))
            return;

        _metaData.SetEntityName(uid, comp.OriginalName);
    }
}

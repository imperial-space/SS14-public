using Content.Server.Actions;
using Content.Server.Imperial.SCP.SCP173.Components;
using Content.Shared.Imperial.SCP.SCP173;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.SCP.SCP173.Systems;

public sealed class SCP173LightFlickerSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SCP173LightFlickerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SCP173LightFlickerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SCP173LightFlickerComponent, SCP173LightFlickerActionEvent>(OnAction);
    }

    private void OnMapInit(Entity<SCP173LightFlickerComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.ActionProto);
    }

    private void OnShutdown(Entity<SCP173LightFlickerComponent> ent, ref ComponentShutdown args)
    {
        RestoreLights(ent);
    }

    private void OnAction(Entity<SCP173LightFlickerComponent> ent, ref SCP173LightFlickerActionEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.IsActive)
            return;

        ent.Comp.IsActive = true;
        ent.Comp.LightsOff = false;
        ent.Comp.EndTime = _timing.CurTime + ent.Comp.Duration;
        ent.Comp.NextToggle = _timing.CurTime;
        ent.Comp.CapturedLightStates.Clear();

        if (ent.Comp.ActivationSound != null)
            _audio.PlayPvs(ent.Comp.ActivationSound, ent);

        args.Handled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<SCP173LightFlickerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.IsActive)
                continue;

            if (curTime >= comp.EndTime)
            {
                RestoreLights((uid, comp));
                comp.IsActive = false;
                comp.LightsOff = false;
                continue;
            }

            if (curTime < comp.NextToggle)
                continue;

            comp.LightsOff = !comp.LightsOff;
            comp.NextToggle = curTime + comp.ToggleInterval;
            ApplyLightState((uid, comp), !comp.LightsOff);
        }
    }

    private void ApplyLightState(Entity<SCP173LightFlickerComponent> ent, bool turnOn)
    {
        var origin = _transform.GetWorldPosition(ent);
        var map = Transform(ent).MapID;

        foreach (var lightUid in _lookup.GetEntitiesInRange(ent, ent.Comp.Radius, LookupFlags.Dynamic | LookupFlags.Static))
        {
            if (!TryComp<PointLightComponent>(lightUid, out var pointLight))
                continue;

            if (!TryComp<TransformComponent>(lightUid, out var lightXform) || lightXform.MapID != map)
                continue;

            var distance = (_transform.GetWorldPosition(lightUid) - origin).Length();
            if (distance > ent.Comp.Radius)
                continue;

            if (!ent.Comp.CapturedLightStates.ContainsKey(lightUid))
                ent.Comp.CapturedLightStates[lightUid] = pointLight.Enabled;

            var targetEnabled = turnOn ? ent.Comp.CapturedLightStates[lightUid] : false;
            _pointLight.SetEnabled(lightUid, targetEnabled, pointLight);
        }
    }

    private void RestoreLights(Entity<SCP173LightFlickerComponent> ent)
    {
        foreach (var (lightUid, originalEnabled) in ent.Comp.CapturedLightStates)
        {
            if (!TryComp<PointLightComponent>(lightUid, out var pointLight))
                continue;

            _pointLight.SetEnabled(lightUid, originalEnabled, pointLight);
        }

        ent.Comp.CapturedLightStates.Clear();
    }
}
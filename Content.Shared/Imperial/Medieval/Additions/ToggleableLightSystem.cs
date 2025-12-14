using Content.Shared.Imperial.ToggleableLight.Components;
using Content.Shared.Interaction;
using Robust.Shared.Audio.Systems;

namespace Content.Shared.Imperial.ToggleableLight.EntitySystems;

public sealed class ToggleableLightSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPointLightSystem _light = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ToggleableLightComponent, ActivateInWorldEvent>(OnActivate);
    }

    private void OnActivate(EntityUid uid, ToggleableLightComponent comp, ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ToggleLight(uid, comp);
    }

    public void ToggleLight(EntityUid uid, ToggleableLightComponent comp)
    {
        if (!_light.TryGetLight(uid, out var light))
            return;

        _light.SetEnabled(uid, !light.Enabled, light);

        var sound = light.Enabled ? comp.TurnOnSound : comp.TurnOffSound;
        if (sound != null)
            _audio.PlayPredicted(sound, uid, uid);
    }
}

using System;
using Content.Client.Audio;
using Content.Shared.Imperial.Medieval.Audio.Events;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Network;

namespace Content.Client.Imperial.Medieval.Audio;

/// <summary>
/// Клиентская система для управления локационными звуками
/// </summary>
public sealed class LocationAudioClientSystem : EntitySystem
{
    [Dependency] private readonly ContentAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<PlayLocationSoundEvent>(OnPlayLocationSound);
        SubscribeNetworkEvent<PlayRandomLocationSoundEvent>(OnPlayRandomLocationSound);
        SubscribeNetworkEvent<StopAllLocationAudioEvent>(OnStopAllLocationAudio);
    }

    private void OnPlayLocationSound(PlayLocationSoundEvent ev)
    {
        _audio.PlayLocationSound(ev.SoundPath, ev.Loop);
    }

    private void OnPlayRandomLocationSound(PlayRandomLocationSoundEvent ev)
    {
        _audio.PlayRandomLocationSound(ev.SoundPath);
    }

    private void OnStopAllLocationAudio(StopAllLocationAudioEvent ev)
    {
        _audio.StopAllLocationAudio();
    }
}

using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Client.Imperial.Cult;

/// <summary>
/// Музыка ритуала Нар'Си (Tear of the Veil). Громкость — «Громкость фоновой музыки» в настройках звука.
/// </summary>
public sealed class CultRitualMusicSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private static readonly SoundPathSpecifier Music = new("/Audio/Imperial/cult/Tear-of-veil.ogg");
    private const float BaseVolume = -4f;

    private EntityUid? _stream;
    private float _slider;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CultRitualMusicEvent>(OnMusic);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(_ => Stop());
        Subs.CVar(_cfg, CCVars.AmbientMusicVolume, OnVolumeChanged, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        Stop();
    }

    private void OnVolumeChanged(float gain)
    {
        _slider = SharedAudioSystem.GainToVolume(gain);
        if (_stream != null)
            _audio.SetVolume(_stream, BaseVolume + _slider);
    }

    private void OnMusic(CultRitualMusicEvent ev)
    {
        Stop();
        var played = _audio.PlayGlobal(Music, Filter.Local(), false, AudioParams.Default.WithVolume(BaseVolume + _slider));
        _stream = played?.Entity;
    }

    private void Stop()
    {
        _stream = _audio.Stop(_stream);
    }
}

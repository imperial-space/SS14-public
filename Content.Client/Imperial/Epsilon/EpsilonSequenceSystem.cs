using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.Epsilon;
using Robust.Client.Graphics;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Epsilon;

/// <summary>
/// Протокол «Эпсилон» у игрока: трек оповещения (громкость — «Громкость фоновой музыки», как у ритуала культа)
/// и надписи, печатающиеся по буквам под музыку.
/// </summary>
public sealed class EpsilonSequenceSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private static readonly SoundPathSpecifier Music = new("/Audio/Imperial/Epsilon/epsilon.ogg");
    private const float BaseVolume = 0f;

    private EntityUid? _stream;
    private float _slider;
    private EpsilonTextOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<EpsilonSequenceStartEvent>(OnStart);
        SubscribeNetworkEvent<EpsilonSequenceStopEvent>(_ => Stop());
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

    private void OnStart(EpsilonSequenceStartEvent ev)
    {
        Stop();

        // Опоздавший к началу клиент вступает с нужного места трека.
        var offset = (float) Math.Max(0, (_timing.CurTime - ev.Start).TotalSeconds);
        var audioParams = AudioParams.Default.WithVolume(BaseVolume + _slider).WithPlayOffset(offset);
        _stream = _audio.PlayGlobal(Music, Filter.Local(), false, audioParams)?.Entity;

        _overlay = new EpsilonTextOverlay(ev.Start);
        _overlays.AddOverlay(_overlay);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_overlay != null && _overlay.Finished)
        {
            _overlays.RemoveOverlay(_overlay);
            _overlay = null;
        }
    }

    private void Stop()
    {
        _stream = _audio.Stop(_stream);
        if (_overlay != null)
        {
            _overlays.RemoveOverlay(_overlay);
            _overlay = null;
        }
    }
}

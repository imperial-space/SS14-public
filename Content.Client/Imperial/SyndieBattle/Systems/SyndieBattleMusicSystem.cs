using Content.Client.Audio;
using Content.Shared.CCVar;
using Content.Client.Gameplay;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.SyndieBattle.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Robust.Client.State;

namespace Content.Client.Imperial.SyndieBattle.Systems;

public sealed class SyndieBattleMusicSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly ContentAudioSystem _contentAudioSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IStateManager _stateManager = default!;

    private EntityUid? _stream;
    private SoundSpecifier? _music;
    private ResPath? _lastTrack;
    private bool _active;
    private bool _enabled;
    private bool _playbackFailed;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesOutsidePrediction = true;
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeNetworkEvent<RoundEndMessageEvent>(OnRoundEnd);
        SubscribeLocalEvent<PlayAmbientMusicEvent>(OnPlayAmbientMusic);
        SubscribeNetworkEvent<SyndieBattleModeStatusEvent>(OnModeStatus);
        Subs.CVar(_configurationManager, CCVars.EventMusicEnabled, OnMusicEnabledChanged, true);
        _stateManager.OnStateChanged += OnStateChanged;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_stateManager.CurrentState is not GameplayState)
        {
            StopMusic();
            return;
        }

        if (!_active || !_enabled || _music == null || _playbackFailed || Exists(_stream))
            return;

        SoundSpecifier selectedTrack;
        if (_music is SoundCollectionSpecifier { Collection: { } collection } collectionSpecifier)
        {
            if (!_prototypeManager.TryIndex<SoundCollectionPrototype>(collection, out var prototype))
            {
                _playbackFailed = true;
                return;
            }

            var tracks = prototype.PickFiles;
            if (tracks.Count == 0)
            {
                _playbackFailed = true;
                return;
            }

            var index = _random.Next(tracks.Count);
            if (tracks.Count > 1 && _lastTrack is { } lastTrack && tracks[index] == lastTrack)
                index = (index + 1) % tracks.Count;

            _lastTrack = tracks[index];
            selectedTrack = new SoundPathSpecifier(tracks[index], collectionSpecifier.Params);
        }
        else
        {
            selectedTrack = _music;
        }

        _stream = _audioSystem.PlayGlobal(selectedTrack, Filter.Local(), false)?.Entity;
        _playbackFailed = _stream == null;
    }

    public override void Shutdown()
    {
        _stateManager.OnStateChanged -= OnStateChanged;
        StopMusic();
        base.Shutdown();
    }

    private void OnModeStatus(SyndieBattleModeStatusEvent message, EntitySessionEventArgs args)
    {
        _active = message.Active;
        _music = message.Music;
        _playbackFailed = false;
        if (!_active || _music == null)
            StopMusic();
        else if (_enabled && _music != null)
            _contentAudioSystem.DisableAmbientMusic();
    }

    private void OnMusicEnabledChanged(bool enabled)
    {
        _enabled = enabled;
        _playbackFailed = false;
        if (!_enabled)
            StopMusic();
        else if (_active && _music != null)
            _contentAudioSystem.DisableAmbientMusic();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _active = false;
        _music = null;
        _lastTrack = null;
        _playbackFailed = false;
        StopMusic();
    }

    private void OnRoundEnd(RoundEndMessageEvent args)
    {
        _active = false;
        StopMusic();
    }

    private void OnPlayAmbientMusic(ref PlayAmbientMusicEvent args)
    {
        if (_active && _enabled && _music != null && !_playbackFailed)
            args.Cancelled = true;
    }

    private void OnStateChanged(StateChangedEventArgs args)
    {
        if (args.NewState is not GameplayState)
            StopMusic();
    }

    private void StopMusic()
    {
        _stream = _audioSystem.Stop(_stream);
    }
}

using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

// ═══════════════════════ Панель ведущего ═══════════════════════

[Serializable, NetSerializable]
public enum CadiaPanelAction : byte
{
    Start,
    NextPhase,
    WaveImperium,
    WaveChaos,
    WinImperium,
    WinChaos,
    BeginFall,
    AnnounceAll,
    AnnounceImperium,
    AnnounceChaos,
    StormBloodRain,
    StormWhispers,
    StormDaemonSurge,
    AddWarp,
    CycleWeather,
    ToggleCloseImperium,
    ToggleCloseChaos,
    RemovePlayer,
    BanPlayer,
    ShowIntro,
    VeilToggle,
    VeilAddMinute,
    VeilCutMinute,
    DropRecharge,
    DropPod,
}

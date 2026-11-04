using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.AssaultPod;

[Serializable, NetSerializable]
public enum AssaultPodState : byte
{
    /// <summary>Стоит на базе, можно садиться.</summary>
    Ready,
    /// <summary>Идёт отсчёт, двери заперты.</summary>
    Launching,
    /// <summary>В полёте.</summary>
    Flying,
    /// <summary>Приземлилась, больше не летает.</summary>
    Landed,
}

using Content.Shared.Atmos;
using Content.Shared.Imperial.Fission;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Fission;

/// <summary>Фабрикатор ядерных стержней (nuclear_rod_fabricator): чертежи из прототипов стержней, 10 секунд на стержень.</summary>
[RegisterComponent, Access(typeof(FissionSystem))]
public sealed partial class FissionFabricatorComponent : Component
{
    [DataField] public bool Upgraded;
    [DataField] public EntProtoId UpgradeDisk = "ImperialFissionFabricatorUpgrade";
    [DataField] public TimeSpan WorkTime = TimeSpan.FromSeconds(10);
    [DataField] public float IdleLoad = 50;
    [DataField] public float ActiveLoad = 3000;
    [ViewVariables] public bool Active;
    [ViewVariables] public TimeSpan EndTime;
    [ViewVariables] public EntProtoId? Schematic;
    [ViewVariables] public TimeSpan? UnpoweredSince;
    [DataField] public SoundSpecifier PingSound = new SoundPathSpecifier("/Audio/Machines/ding.ogg");
    [DataField] public SoundSpecifier BuzzSound = new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg");
}

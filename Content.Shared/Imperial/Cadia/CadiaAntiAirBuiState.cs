using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

[Serializable, NetSerializable]
public sealed class CadiaAntiAirBuiState : BoundUserInterfaceState
{
    public string GunName = string.Empty;
    public bool Linked;
    public float Range;
    public int Levels;
    public int AimLevel;
    public Angle Aim;
    public Angle TargetAim;
    public Vector2? AimPoint;
    public bool Firing;
    public int Ammo;
    public int MaxAmmo;
    public bool AuspexLinked;
    public List<CadiaAirContact> Contacts = new();
    public List<CadiaAirBurst> Bursts = new();

    /// <summary>Борт, на котором захвачена точка прицеливания, и в дальности ли он.</summary>
    public NetEntity? AimGrid;
    public bool OutOfRange;

    /// <summary>Борт, за которым сейчас следит камера наводчика.</summary>
    public NetEntity? Viewing;
}

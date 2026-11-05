using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics;

[Serializable, NetSerializable]
public sealed class DnaConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    // Сканер
    public bool IsScannerConnected;
    public bool IsViableSubject;
    public bool ScannerLocked;
    public bool ScannerOpen;
    public bool HasDelayedAction;
    public string SubjectName = string.Empty;
    public DnaSubjectStatus SubjectStatus;
    public float SubjectHealth;
    public float SubjectDamage;
    public bool IsMonkey;
    public string SubjectUniqueIdentity = string.Empty;
    public string SubjectUniqueFeatures = string.Empty;

    // Откаты
    public bool IsScrambleReady;
    public int ScrambleSeconds;
    public bool IsJokerReady;
    public int JokerSeconds;
    public bool IsInjectorReady;
    public int InjectorSeconds;
    public bool IsPulsing;
    public int TimeToPulse;
    public int GeneticMakeupCooldown;
    public int CrisprCharges;

    // Хранилища
    public List<DnaMutationState> Occupant = new();
    public List<DnaMutationState> Console = new();
    public List<DnaMutationState> Disk = new();
    public List<DnaAdvancedInjectorState> Injectors = new();
    public int MaxAdvInjectors;
    public Dictionary<ChromosomeKind, int> Chromosomes = new();

    // Дискета
    public bool HasDisk;
    public bool DiskReadOnly;
    public int DiskCapacity;
    public DnaMakeupState? DiskMakeup;

    // Ферменты
    public int PulseStrength;
    public int PulseDuration;
    public string StdDevAcc = string.Empty;
    public int StdDevStr;
    public DnaMakeupState?[] MakeupStorage = new DnaMakeupState?[3];
}

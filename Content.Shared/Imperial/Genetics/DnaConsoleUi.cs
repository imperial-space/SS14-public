using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics;

[Serializable, NetSerializable]
public enum DnaConsoleUiKey : byte
{
    Key,
}

/// <summary>Откуда мутация в окне консоли (mutation.Source в tgui SS13).</summary>
[Serializable, NetSerializable]
public enum DnaMutationSource : byte
{
    Occupant,
    Console,
    Disk,
    Injector,
}

/// <summary>Класс мутации пациента: из генома, добавлена сверху (MUT_EXTRA), из других источников (MUT_OTHER).</summary>
[Serializable, NetSerializable]
public enum DnaMutationClass : byte
{
    Normal,
    Extra,
    Other,
}

/// <summary>Можно ли вставить хромосому (CHROMOSOME_NEVER / NONE / USED).</summary>
[Serializable, NetSerializable]
public enum DnaChromosomeState : byte
{
    Never,
    None,
    Used,
}

[Serializable, NetSerializable]
public enum DnaSubjectStatus : byte
{
    Conscious,
    SoftCrit,
    Unconscious,
    HardCrit,
    Dead,
    Transforming,
}

/// <summary>Действие с позицией гена (pulse_gene): следующее основание, предыдущее или X.</summary>
[Serializable, NetSerializable]
public enum DnaGeneAction : byte
{
    Clear,
    Next,
    Prev,
}

/// <summary>Часть генетического облика: ферменты (имя и ДНК), идентичность (внешность), особенности, всё сразу.</summary>
[Serializable, NetSerializable]
public enum DnaMakeupType : byte
{
    Enzymes,
    Identity,
    Features,
    Mixed,
}

/// <summary>Ссылка на мутацию в окне консоли.</summary>
[Serializable, NetSerializable]
public readonly record struct DnaMutationRef(DnaMutationSource Source, int Index, int Injector = -1);

/// <summary>Мутация в окне консоли.</summary>
[Serializable, NetSerializable]
public sealed class DnaMutationState
{
    public DnaMutationRef Ref;
    public ProtoId<GeneticMutationPrototype> Mutation;
    public string Alias = string.Empty;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public MutationQuality Quality;
    public int Instability;
    public bool Discovered;
    public bool Active;
    public DnaMutationClass Class;
    public string Sequence = string.Empty;
    public string DefaultSequence = string.Empty;
    public DnaChromosomeState CanChromo;
    public ChromosomeKind AppliedChromo;
    public ChromosomeKind ValidChromos;
}

[Serializable, NetSerializable]
public sealed class DnaAdvancedInjectorState
{
    public string Name = string.Empty;
    public List<DnaMutationState> Mutations = new();
}

/// <summary>Генетический облик (genetic makeup): имя, ДНК и строки ферментов.</summary>
[Serializable, NetSerializable]
public sealed class DnaMakeupState
{
    public string Name = string.Empty;
    public string BloodType = string.Empty;
    public string UniqueEnzymes = string.Empty;
    public string UniqueIdentity = string.Empty;
    public string UniqueFeatures = string.Empty;
}

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

#region Сообщения

[Serializable, NetSerializable]
public sealed class DnaConsoleScrambleMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DnaConsoleToggleLockMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DnaConsoleToggleDoorMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DnaConsoleCancelDelayMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DnaConsoleEjectDiskMessage : BoundUserInterfaceMessage;

/// <summary>Изменить позицию гена пациента. С «Джокером» позиция открывается верной.</summary>
[Serializable, NetSerializable]
public sealed class DnaConsolePulseGeneMessage(ProtoId<GeneticMutationPrototype> mutation, int position, DnaGeneAction action, bool joker)
    : BoundUserInterfaceMessage
{
    public readonly ProtoId<GeneticMutationPrototype> Mutation = mutation;
    public readonly int Position = position;
    public readonly DnaGeneAction Action = action;
    public readonly bool Joker = joker;
}

[Serializable, NetSerializable]
public sealed class DnaConsoleApplyChromoMessage(DnaMutationRef mutation, ChromosomeKind kind) : BoundUserInterfaceMessage
{
    public readonly DnaMutationRef Mutation = mutation;
    public readonly ChromosomeKind Kind = kind;
}

[Serializable, NetSerializable]
public sealed class DnaConsoleEjectChromoMessage(ChromosomeKind kind) : BoundUserInterfaceMessage
{
    public readonly ChromosomeKind Kind = kind;
}

[Serializable, NetSerializable]
public sealed class DnaConsolePrintInjectorMessage(DnaMutationRef mutation, bool activator) : BoundUserInterfaceMessage
{
    public readonly DnaMutationRef Mutation = mutation;
    public readonly bool Activator = activator;
}

[Serializable, NetSerializable]
public sealed class DnaConsoleSaveConsoleMessage(DnaMutationRef mutation) : BoundUserInterfaceMessage
{
    public readonly DnaMutationRef Mutation = mutation;
}

[Serializable, NetSerializable]
public sealed class DnaConsoleSaveDiskMessage(DnaMutationRef mutation) : BoundUserInterfaceMessage
{
    public readonly DnaMutationRef Mutation = mutation;
}

[Serializable, NetSerializable]
public sealed class DnaConsoleDeleteMutationMessage(DnaMutationRef mutation) : BoundUserInterfaceMessage
{
    public readonly DnaMutationRef Mutation = mutation;
}

/// <summary>Снять с пациента мутацию, которой нет в его геноме (nullify).</summary>
[Serializable, NetSerializable]
public sealed class DnaConsoleNullifyMessage(ProtoId<GeneticMutationPrototype> mutation) : BoundUserInterfaceMessage
{
    public readonly ProtoId<GeneticMutationPrototype> Mutation = mutation;
}

[Serializable, NetSerializable]
public sealed class DnaConsoleCombineMessage(DnaMutationRef first, DnaMutationRef second) : BoundUserInterfaceMessage
{
    public readonly DnaMutationRef First = first;
    public readonly DnaMutationRef Second = second;
}

[Serializable, NetSerializable]
public sealed class DnaConsoleAddAdvInjMessage(DnaMutationRef mutation, string injector) : BoundUserInterfaceMessage
{
    public readonly DnaMutationRef Mutation = mutation;
    public readonly string Injector = injector;
}

[Serializable, NetSerializable]
public sealed class DnaConsoleNewAdvInjMessage(string name) : BoundUserInterfaceMessage
{
    public readonly string Name = name;
}

[Serializable, NetSerializable]
public sealed class DnaConsoleDeleteAdvInjMessage(string name) : BoundUserInterfaceMessage
{
    public readonly string Name = name;
}

[Serializable, NetSerializable]
public sealed class DnaConsolePrintAdvInjMessage(string name) : BoundUserInterfaceMessage
{
    public readonly string Name = name;
}

[Serializable, NetSerializable]
public sealed class DnaConsoleSetPulseStrengthMessage(int value) : BoundUserInterfaceMessage
{
    public readonly int Value = value;
}

[Serializable, NetSerializable]
public sealed class DnaConsoleSetPulseDurationMessage(int value) : BoundUserInterfaceMessage
{
    public readonly int Value = value;
}

/// <summary>Импульс по символу уникальной идентичности (ui) или особенностей (uf).</summary>
[Serializable, NetSerializable]
public sealed class DnaConsoleMakeupPulseMessage(bool features, int index) : BoundUserInterfaceMessage
{
    public readonly bool Features = features;
    public readonly int Index = index;
}

[Serializable, NetSerializable]
public sealed class DnaConsoleSaveMakeupMessage(int index) : BoundUserInterfaceMessage
{
    public readonly int Index = index;
}

[Serializable, NetSerializable]
public sealed class DnaConsoleDeleteMakeupMessage(int index) : BoundUserInterfaceMessage
{
    public readonly int Index = index;
}

/// <summary>Перенести облик из буфера на пациента; без пациента — отложенно.</summary>
[Serializable, NetSerializable]
public sealed class DnaConsoleApplyMakeupMessage(int index, DnaMakeupType type) : BoundUserInterfaceMessage
{
    public readonly int Index = index;
    public readonly DnaMakeupType Type = type;
}

[Serializable, NetSerializable]
public sealed class DnaConsoleMakeupInjectorMessage(int index, DnaMakeupType type) : BoundUserInterfaceMessage
{
    public readonly int Index = index;
    public readonly DnaMakeupType Type = type;
}

[Serializable, NetSerializable]
public sealed class DnaConsoleSaveMakeupDiskMessage(int index) : BoundUserInterfaceMessage
{
    public readonly int Index = index;
}

[Serializable, NetSerializable]
public sealed class DnaConsoleLoadMakeupDiskMessage(int index) : BoundUserInterfaceMessage
{
    public readonly int Index = index;
}

[Serializable, NetSerializable]
public sealed class DnaConsoleDeleteDiskMakeupMessage : BoundUserInterfaceMessage;

#endregion

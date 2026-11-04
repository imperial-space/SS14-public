using System.Linq;
using System.Text;
using Content.Server.Chat.Managers;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Imperial.Genetics;
using Content.Shared.Imperial.Genetics.Components;
using Content.Shared.Imperial.Genetics.Prototypes;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Genetics;

/// <summary>Ручной сканер генетических последовательностей (sequence_scanner из SS13).</summary>
[RegisterComponent]
public sealed partial class SequenceScannerComponent : Component
{
    /// <summary>Мутации последнего просканированного существа и активны ли они.</summary>
    [ViewVariables]
    public List<(ProtoId<GeneticMutationPrototype> Mutation, bool Active)> Buffer = new();

    [ViewVariables]
    public GeneticMakeupData? Makeup;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(20);

    [DataField]
    public TimeSpan MakeupScanTime = TimeSpan.FromSeconds(3);

    [ViewVariables]
    public TimeSpan ReadyAt;

    [DataField]
    public SoundSpecifier ScanSound = new SoundPathSpecifier("/Audio/Items/Medical/healthscanner.ogg");
}

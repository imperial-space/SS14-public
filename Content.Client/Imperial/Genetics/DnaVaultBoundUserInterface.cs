using System.Numerics;
using Content.Shared.Imperial.Genetics;
using Content.Shared.Imperial.Genetics.Prototypes;
using JetBrains.Annotations;
using Content.Client.Imperial.UI;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Genetics;

[UsedImplicitly]
public sealed class DnaVaultBoundUserInterface : BoundUserInterface
{
    private DnaVaultWindow? _window;

    public DnaVaultBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DnaVaultWindow>();
        _window.OnChoose += mutation => SendMessage(new DnaVaultChooseMessage(mutation));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DnaVaultBoundUserInterfaceState vault)
            _window?.UpdateState(vault);
    }
}

/// <summary>Окно ДНК-хранилища 1 в 1 с tgui DnaVault из SS13 (350×400).</summary>
public sealed class DnaVaultWindow : DefaultWindow
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public event Action<ProtoId<GeneticMutationPrototype>>? OnChoose;

    private readonly BoxContainer _root = Tgui.VBox(8);

    public DnaVaultWindow()
    {
        IoCManager.InjectDependencies(this);
        Title = Loc.GetString("dna-vault-title");
        MinSize = SetSize = new Vector2(350, 400);
        _root.Margin = new Thickness(6);
        Contents.AddChild(Tgui.Window(_root));
    }

    public void UpdateState(DnaVaultBoundUserInterfaceState state)
    {
        _root.RemoveAllChildren();

        _root.AddChild(Tgui.MakeSection(Loc.GetString("dna-vault-database"), null, Tgui.LabeledList(
            (Loc.GetString("dna-vault-human-dna"), Samples(state.Dna, state.DnaMax)),
            (Loc.GetString("dna-vault-plant-dna"), Samples(state.Plants, state.PlantsMax)),
            (Loc.GetString("dna-vault-animal-dna"), Samples(state.Animals, state.AnimalsMax)))));

        if (!state.Completed)
            return;

        // Секция генной терапии видна только тем, кто ещё не выбрал улучшение (completed && !used).
        var local = _player.LocalEntity is { } player ? _entMan.GetNetEntity(player) : NetEntity.Invalid;
        if (!state.Choices.TryGetValue(local, out var choices) || choices.Count == 0)
            return;

        var buttons = Tgui.HBox(4);
        foreach (var choice in choices)
        {
            var id = choice;
            var name = _proto.TryIndex(id, out var proto) ? Loc.GetString(proto.Name) : id.Id;
            var button = Tgui.Button(name, () => OnChoose?.Invoke(id), fluid: true, bold: true,
                tooltip: proto?.Description is { } description ? Loc.GetString(description) : null);
            buttons.AddChild(button);
        }

        _root.AddChild(Tgui.MakeSection(Loc.GetString("dna-vault-gene-therapy"), null, Tgui.VBox(6,
            Tgui.Text(Loc.GetString("dna-vault-applicable"), bold: true, align: Control.HAlignment.Center),
            buttons)));
    }

    /// <summary>ProgressBar «N / M Samples».</summary>
    private static Control Samples(int value, int max)
    {
        var ratio = max > 0 ? value / (float) max : 0;
        return Tgui.ProgressBar(ratio, Tgui.Primary, Loc.GetString("dna-vault-samples", ("value", value), ("max", max)));
    }
}

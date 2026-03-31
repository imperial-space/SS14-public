using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Content.Shared.Administration;
using System.Numerics;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Client.Administration.UI.BloodBrotherSelection;

public sealed class BloodBrotherSelectionWindow : DefaultWindow
{
    public event Action<NetUserId>? OnConfirmed;

    private NetUserId? _selected;

    public BloodBrotherSelectionWindow()
    {
        Title = Loc.GetString("blood-brother-select-title");

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6
        };
        Contents.AddChild(root);

        Description = new Label();
        root.AddChild(Description);

        CandidateList = new ItemList
        {
            SelectMode = ItemList.ItemListSelectMode.Single,
            VerticalExpand = true,
            MinSize = new Vector2(260, 220)
        };
        root.AddChild(CandidateList);

        var buttons = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6
        };
        root.AddChild(buttons);

        ConfirmButton = new Button
        {
            Text = Loc.GetString("blood-brother-select-confirm"),
            Disabled = true,
            HorizontalExpand = true
        };
        buttons.AddChild(ConfirmButton);

        var cancelButton = new Button
        {
            Text = Loc.GetString("blood-brother-select-cancel"),
            HorizontalExpand = true
        };
        buttons.AddChild(cancelButton);

        CandidateList.OnItemSelected += OnItemSelected;
        CandidateList.OnItemDeselected += _ =>
        {
            _selected = null;
            ConfirmButton.Disabled = true;
        };

        ConfirmButton.OnPressed += _ =>
        {
            if (_selected != null)
                OnConfirmed?.Invoke(_selected.Value);
        };

        cancelButton.OnPressed += _ => Close();
    }

    public Label Description { get; }
    public ItemList CandidateList { get; }
    public Button ConfirmButton { get; }

    public void SetState(BloodBrotherSelectionEuiState state)
    {
        Description.Text = Loc.GetString("blood-brother-select-label", ("target", state.TargetName));
        CandidateList.Clear();
        _selected = null;
        ConfirmButton.Disabled = true;

        if (state.Players.Count == 0)
        {
            var empty = new ItemList.Item(CandidateList)
            {
                Text = Loc.GetString("blood-brother-select-empty")
            };
            CandidateList.Add(empty);
            return;
        }

        foreach (var player in state.Players)
        {
            var item = new ItemList.Item(CandidateList)
            {
                Text = player.Name,
                Metadata = player.UserId
            };
            CandidateList.Add(item);
        }
    }

    private void OnItemSelected(ItemList.ItemListSelectedEventArgs args)
    {
        if (args.ItemList[args.ItemIndex].Metadata is not NetUserId userId)
            return;

        _selected = userId;
        ConfirmButton.Disabled = false;
    }
}
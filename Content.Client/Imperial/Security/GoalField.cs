using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Security;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Security;

/// <summary>Поле цели в очках с кнопкой «Задать».</summary>
internal sealed class GoalField
{
    public readonly LineEdit Edit = new() { HorizontalExpand = true };
    private readonly Button _set = new() { Text = Loc.GetString("gulag-console-set-goal") };
    private int? _lastServerGoal;

    public event Action<int>? Submitted;

    public GoalField()
    {
        Edit.OnTextEntered += _ => Submit();
        _set.OnPressed += _ => Submit();
    }

    /// <summary>Значение с сервера пишется в поле только если оно изменилось и поле не редактируется.</summary>
    public void SetServerGoal(int goal)
    {
        if (_lastServerGoal == goal)
            return;

        _lastServerGoal = goal;
        if (!Edit.HasKeyboardFocus())
            Edit.Text = goal.ToString();
    }

    public BoxContainer BuildRow()
    {
        (Edit.Parent as Control)?.RemoveChild(Edit);
        (_set.Parent as Control)?.RemoveChild(_set);

        var row = SecurityUi.Row();
        row.AddChild(new Label { Text = Loc.GetString("gulag-console-goal") + ":", MinWidth = 140 });
        row.AddChild(Edit);
        row.AddChild(_set);
        return row;
    }

    private void Submit()
    {
        if (int.TryParse(Edit.Text.Trim(), out var goal))
            Submitted?.Invoke(goal);
    }
}

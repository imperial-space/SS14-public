using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;
using Content.Shared.Paper;
using static Content.Shared.Paper.PaperComponent;
using Robust.Client.Player;
using Content.Client.Imperial.Medieval.Skills;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Client.Imperial.Medieval.Factions.UI;
using Content.Shared.Imperial.Medieval.Factions;

namespace Content.Client.Paper.UI;

[UsedImplicitly]
public sealed class PaperBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private PaperWindow? _window;

    public PaperBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<PaperWindow>();
        _window.OnSaved += InputOnTextEntered;

        if (EntMan.TryGetComponent<PaperComponent>(Owner, out var paper))
        {
            _window.MaxInputLength = paper.ContentSize;
        }
        if (EntMan.TryGetComponent<PaperVisualsComponent>(Owner, out var visuals))
        {
            _window.InitVisuals(Owner, visuals);
        }

        // Imperial Medieval start
        _window.SetRelations(null);

        if (EntMan.TryGetComponent<MedievalFactionRelationsRequestComponent>(Owner, out var request))
        {
            var player = IoCManager.Resolve<IPlayerManager>().LocalEntity;
            if (!EntMan.TryGetComponent<MedievalFactionMemberComponent>(player, out var member) || member.Faction != request.To || member.MenuAccess != FactionMenuAccess.Full)
                return;

            _window.SetRelations(request);
            _window.SendRelations += arg =>
            {
                EntityEventArgs ev = new SetFactionRelationsByRequestEvent(EntMan.GetNetEntity(Owner), !arg);
                EntMan.RaisePredictiveEvent(ev);
                Close();
            };
        }
        // Imperial Medieval end
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        // Imperial Medieval Skills start
        bool canRead = true;
        var player = IoCManager.Resolve<IPlayerManager>().LocalEntity;
        if (!player.HasValue || !IoCManager.Resolve<IEntityManager>().System<SkillsSystem>().CanRead(player.Value))
            canRead = false;
        // Imperial Medieval Skills end

        _window?.Populate((PaperBoundUserInterfaceState) state, canRead);   // Imperial Medieval - canRead added
    }

    private void InputOnTextEntered(string text)
    {
        SendMessage(new PaperInputTextMessage(text));

        if (_window != null)
        {
            _window.Input.TextRope = Rope.Leaf.Empty;
            _window.Input.CursorPosition = new TextEdit.CursorPos(0, TextEdit.LineBreakBias.Top);
        }
    }
}

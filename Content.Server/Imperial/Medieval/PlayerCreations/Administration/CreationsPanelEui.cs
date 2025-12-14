using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Content.Server.Administration;
using Content.Shared.Imperial.Medieval.PlayerCreations;

namespace Content.Server.Imperial.Medieval.PlayerCreations.Administration;

public sealed class CreationsPanelEui : BaseEui
{
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    private readonly CreationsSystem _creationsSystem;

    public CreationsPanelEui()
    {
        IoCManager.InjectDependencies(this);
        _creationsSystem = _entitySystemManager.GetEntitySystem<CreationsSystem>();
    }
    public override void Opened()
    {
        base.Opened();
        _creationsSystem.RegisterEui(this);
        _adminManager.OnPermsChanged += OnPermsChanged;
    }

    public override void Closed()
    {
        base.Closed();

        _creationsSystem.UnregisterEui(this);
        _adminManager.OnPermsChanged -= OnPermsChanged;
    }


    private void OnPermsChanged(AdminPermsChangedEventArgs args)
    {
        if (args.Player == Player && !_adminManager.HasAdminFlag(Player, AdminFlags.Moderator))
        {
            Close();
        }
    }

    public void SendNewIncomingPainting(CreationPaintingMessage painting)
    {
        SendMessage(new NewIncomingCreationPaintingMessage(painting));
    }

    public void SendRemoveIncomingPainting(CreationPaintingMessage painting)
    {
        SendMessage(new RemoveIncomingCreationPaintingMessage(painting));
    }

    public void SendNewAcceptedPainting(CreationPaintingMessage painting)
    {
        SendMessage(new NewAcceptedCreationPaintingMessage(painting));
    }

    public void SendRemoveAcceptedPainting(CreationPaintingMessage painting)
    {
        SendMessage(new RemoveAcceptedCreationPaintingMessage(painting));
    }


    public void SendNewIncomingBook(CreationBook book)
    {
        SendMessage(new NewIncomingCreationBook(book));
    }

    public void SendRemoveIncomingBook(CreationBook book)
    {
        SendMessage(new RemoveIncomingCreationBook(book));
    }

    public void SendNewAcceptedBook(CreationBook book)
    {
        SendMessage(new NewAcceptedCreationBook(book));
    }

    public void SendRemoveAcceptedBook(CreationBook book)
    {
        SendMessage(new RemoveAcceptedCreationBook(book));
    }


    public override async void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (!_adminManager.HasAdminFlag(Player, AdminFlags.Fun))
            return;


        switch (msg)
        {
            case RequestIncomingCreationPaintingsMessage:
                SendMessage(new ResponseIncomingCreationPaintingsMessage(await _creationsSystem.GetIncomingPaintingsMessages()));
                break;
            case RemoveIncomingCreationPaintingMessage remove:
                _creationsSystem.RemoveIncomingPainting(remove.Painting);
                break;
            case AcceptIncomingCreationPaintingMessage accept:
                _creationsSystem.AcceptIncomingPainting(accept.Painting);
                break;
            case RequestAcceptedCreationPaintingsMessage:
                SendMessage(new ResponseAcceptedCreationPaintingsMessage(await _creationsSystem.GetAcceptedPaintingsMessages()));
                break;
            case RemoveAcceptedCreationPaintingMessage remove:
                _creationsSystem.RemoveAcceptedPainting(remove.Painting);
                break;

            case RequestIncomingCreationBooks:
                SendMessage(new ResponseIncomingCreationBooks(await _creationsSystem.GetIncomingCreationBooks()));
                break;
            case RemoveIncomingCreationBook remove:
                _creationsSystem.RemoveIncomingBook(remove.Book);
                break;
            case AcceptIncomingCreationBook accept:
                _creationsSystem.AcceptIncomingBook(accept.Book);
                break;
            case RequestAcceptedCreationBooks:
                SendMessage(new ResponseAcceptedCreationBooks(await _creationsSystem.GetAcceptedCreationBooks()));
                break;
            case RemoveAcceptedCreationBook remove:
                _creationsSystem.RemoveAcceptedBook(remove.Book);
                break;
        }
    }


}

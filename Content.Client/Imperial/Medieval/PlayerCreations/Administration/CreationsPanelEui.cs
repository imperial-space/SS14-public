using Content.Client.Eui;
using Content.Shared.Eui;
using Content.Shared.Imperial.Medieval.PlayerCreations;
using Robust.Client.Graphics;

namespace Content.Client.Imperial.Medieval.PlayerCreations.Administration;

public sealed class CreationsPanelEui : BaseEui
{
    [Dependency] private readonly IClyde _clyde = default!;


    private readonly Dictionary<CreationPaintingMessage, PaintingEntry> _incomingPaintings = new();
    private readonly Dictionary<CreationPaintingMessage, PaintingEntry> _acceptedPaintings = new();

    private readonly Dictionary<CreationBook, BookEntry> _incomingBooks = new();
    private readonly Dictionary<CreationBook, BookEntry> _acceptedBooks = new();

    private CreationsPanel? _creationsPanel;


    public CreationsPanelEui()
    {
        IoCManager.InjectDependencies(this);
        _creationsPanel = new CreationsPanel();
        _creationsPanel.OnClose += OnCloseWindow;
    }

    private void OnCloseWindow()
    {
        SendMessage(new CloseEuiMessage());
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
        HandlePaintings(msg);
        HandleBooks(msg);

    }

    #region Paintings

    public void HandlePaintings(EuiMessageBase msg)
    {
        switch (msg)
        {
            case ResponseIncomingCreationPaintingsMessage responseIncoming:
                foreach (var painting in responseIncoming.Paintings)
                {
                    TryCreateIncomingPainting(painting);
                }
                break;
            case NewIncomingCreationPaintingMessage newIncoming:
                TryCreateIncomingPainting(newIncoming.Painting);
                break;
            case RemoveIncomingCreationPaintingMessage removeIncoming:
                RemoveIncomingPainting(removeIncoming.Painting);
                break;
            case ResponseAcceptedCreationPaintingsMessage responseAccepted:
                foreach (var painting in responseAccepted.Paintings)
                {
                    TryCreateAcceptedPainting(painting);
                }
                break;
            case NewAcceptedCreationPaintingMessage newAccepted:
                TryCreateAcceptedPainting(newAccepted.Painting);
                break;
            case RemoveAcceptedCreationPaintingMessage removeAccepted:
                RemoveAcceptedPainting(removeAccepted.Painting);
                break;
        }
    }

    private void SendRemoveIncomingPainting(CreationPaintingMessage painting)
    {
        SendMessage(new RemoveIncomingCreationPaintingMessage(painting));
    }

    private void SendAcceptPainting(CreationPaintingMessage painting)
    {
        SendMessage(new AcceptIncomingCreationPaintingMessage(painting));
    }

    private void SendRemoveAcceptedPainting(CreationPaintingMessage painting)
    {
        Logger.Debug("removed");
        SendMessage(new RemoveAcceptedCreationPaintingMessage(painting));
    }

    private bool TryCreateIncomingPainting(CreationPaintingMessage painting)
    {
        if (_incomingPaintings.ContainsKey(painting))
            return false;
        var entry = new PaintingEntry(_clyde, painting, () => SendAcceptPainting(painting), () => SendRemoveIncomingPainting(painting));
        _incomingPaintings.Add(painting, entry);
        _creationsPanel?.IncomingPaintingsTab.AddEntry(entry);

        return true;
    }

    private void RemoveIncomingPainting(CreationPaintingMessage painting)
    {
        if (!_incomingPaintings.TryGetValue(painting, out var entry))
            return;

        _creationsPanel?.IncomingPaintingsTab.RemoveEntry(entry);
        _incomingPaintings.Remove(painting);
    }

    private bool TryCreateAcceptedPainting(CreationPaintingMessage painting)
    {
        if (_acceptedPaintings.ContainsKey(painting))
            return false;
        var entry = new PaintingEntry(_clyde, painting, null, () => ConfirmRemovePainting(painting), false);
        _acceptedPaintings.Add(painting, entry);
        _creationsPanel?.AcceptedPaintingsTab.AddEntry(entry);

        return true;
    }

    private void ConfirmRemovePainting(CreationPaintingMessage painting)
    {
        var confirmDialog = new CreationsConfirmDialog();
        confirmDialog.OnConfirmButtonPressed(() => SendRemoveAcceptedPainting(painting));
        confirmDialog.OpenCentered();
    }

    private void RemoveAcceptedPainting(CreationPaintingMessage painting)
    {
        Logger.Debug("REMOEDs");

        if (!_acceptedPaintings.TryGetValue(painting, out var entry))
            return;

        Logger.Debug("REMOED");

        _creationsPanel?.AcceptedPaintingsTab.RemoveEntry(entry);
        _acceptedPaintings.Remove(painting);
    }
    #endregion


    #region Books

    public void HandleBooks(EuiMessageBase msg)
    {
        switch (msg)
        {
            case ResponseIncomingCreationBooks responseIncoming:
                foreach (var book in responseIncoming.Books)
                {
                    TryCreateIncomingBook(book);
                }
                break;
            case NewIncomingCreationBook newIncoming:
                TryCreateIncomingBook(newIncoming.Book);
                break;
            case RemoveIncomingCreationBook removeIncoming:
                RemoveIncomingBook(removeIncoming.Book);
                break;
            case ResponseAcceptedCreationBooks responseAccepted:
                foreach (var book in responseAccepted.Books)
                {
                    TryCreateAcceptedBook(book);
                }
                break;
            case NewAcceptedCreationBook newAccepted:
                TryCreateAcceptedBook(newAccepted.Book);
                break;
            case RemoveAcceptedCreationBook removeAccepted:
                RemoveAcceptedBook(removeAccepted.Book);
                break;
        }
    }

    private void SendRemoveIncomingBook(CreationBook book)
    {
        SendMessage(new RemoveIncomingCreationBook(book));
    }

    private void SendAcceptBook(CreationBook book)
    {
        SendMessage(new AcceptIncomingCreationBook(book));
    }

    private void SendRemoveAcceptedBook(CreationBook book)
    {
        SendMessage(new RemoveAcceptedCreationBook(book));
    }

    private bool TryCreateIncomingBook(CreationBook book)
    {
        if (_incomingBooks.ContainsKey(book))
            return false;
        var entry = new BookEntry(book, () => SendAcceptBook(book), () => SendRemoveIncomingBook(book));
        _incomingBooks.Add(book, entry);
        _creationsPanel?.IncomingBooksTab.AddEntry(entry);

        return true;
    }

    private void RemoveIncomingBook(CreationBook book)
    {
        if (!_incomingBooks.TryGetValue(book, out var entry))
            return;

        _creationsPanel?.IncomingBooksTab.RemoveEntry(entry);
        _incomingBooks.Remove(book);
    }

    private bool TryCreateAcceptedBook(CreationBook book)
    {
        if (_acceptedBooks.ContainsKey(book))
            return false;
        var entry = new BookEntry(book, null, () => ConfirmRemoveBook(book), false);
        _acceptedBooks.Add(book, entry);
        _creationsPanel?.AcceptedBooksTab.AddEntry(entry);

        return true;
    }


    private void ConfirmRemoveBook(CreationBook book)
    {
        var confirmDialog = new CreationsConfirmDialog();
        confirmDialog.OnConfirmButtonPressed(() => SendRemoveAcceptedBook(book));
        confirmDialog.OpenCentered();
    }

    private void RemoveAcceptedBook(CreationBook book)
    {
        if (!(_acceptedBooks).TryGetValue(book, out var entry))
            return;

        _creationsPanel?.AcceptedBooksTab.RemoveEntry(entry);
        _acceptedBooks.Remove(book);
    }
    #endregion


    public override void Opened()
    {
        base.Opened();

        _creationsPanel?.OpenCenteredLeft();
        SendMessage(new RequestIncomingCreationPaintingsMessage());
        SendMessage(new RequestAcceptedCreationPaintingsMessage());
        SendMessage(new RequestIncomingCreationBooks());
        SendMessage(new RequestAcceptedCreationBooks());
    }

    public override void Closed()
    {
        base.Closed();

        _creationsPanel?.Dispose();
    }
}

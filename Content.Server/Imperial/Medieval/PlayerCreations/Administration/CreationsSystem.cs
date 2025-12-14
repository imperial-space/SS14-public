using System.Linq;
using Content.Server.Database;
using Robust.Shared.Network;
using System.Threading.Tasks;
using Content.Shared.Imperial.Medieval.PlayerCreations;

namespace Content.Server.Imperial.Medieval.PlayerCreations.Administration;

public sealed partial class CreationsSystem : EntitySystem
{
    [Dependency] private readonly IServerDbManager _db = default!;


    private readonly List<CreationsPanelEui> _activeEuis = new();

    public void RegisterEui(CreationsPanelEui eui)
    {
        _activeEuis.Add(eui);
    }

    public void UnregisterEui(CreationsPanelEui eui)
    {
        _activeEuis.Remove(eui);
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SendCreationPaintingEvent>(OnSendPainting);
        SubscribeNetworkEvent<SendCreationBookEvent>(OnSendBook);
    }

    private async Task<string> GetPlayerName(NetUserId id)
    {
        var data = await _db.GetPlayerRecordByUserId(id);

        var name = "";
        if (data != null)
            name = data.LastSeenUserName;

        return name;
    }

    private async Task<List<CreationPaintingMessage>> ToPaintingMessages(List<Painting> dbPaintings)
    {
        var messages = new List<CreationPaintingMessage>();

        foreach (var p in dbPaintings)
        {
            var name = await GetPlayerName((NetUserId)p.AuthorUserId);

            messages.Add(new CreationPaintingMessage(
                PaintingHelper.StringToColors(p.Texture),
                p.Name,
                p.Description,
                p.Author,
                (NetUserId)p.AuthorUserId,
                p.CreationTime,
                name));
        }

        return messages;

    }

    private List<CreationBook> ToBookMessages(List<Book> dbBooks)
    {
        return dbBooks.Select(p =>
            new CreationBook(
                p.Text,
                p.Name,
                p.Description,
                p.Author,
                (NetUserId)p.AuthorUserId,
                p.CreationTime)).ToList();
    }

    private bool ValidatePaintingInput(Color[] painting, string name, string desc)
    {
        return painting.Length < 2000 &&
               name.Length < 40 &&
               desc.Length < 500;
    }

    private bool ValidateBookInput(string text, string name, string desc)
    {
        return text.Length < 12000 &&
               name.Length < 40 &&
               desc.Length < 500;
    }

    private async Task<bool> AddIncoming<T>(
        T creation,
        Func<T, Task<bool>> existsCheck,
        Func<T, Task> addToDb,
        Action<CreationsPanelEui, T> notifyEui)
    {
        if (await existsCheck(creation))
            return false;

        await addToDb(creation);

        foreach (var eui in _activeEuis)
            notifyEui(eui, creation);

        return true;
    }

    private async Task ProcessIncoming<T>(
        T creation,
        Func<T, Task> dbAction,
        Action<CreationsPanelEui, T> notifyEui)
    {
        await dbAction(creation);

        foreach (var eui in _activeEuis)
            notifyEui(eui, creation);
    }




    #region Paintings
    private async void OnSendPainting(SendCreationPaintingEvent args)
    {
        if (!ValidatePaintingInput(args.Painting, args.Name, args.Description))
            return;

        var authorName = await GetPlayerName(args.SenderPlayer);

        var paintingMessage = new CreationPaintingMessage(
            args.Painting,
            args.Name,
            args.Description,
            args.Author,
            args.SenderPlayer,
            DateTime.UtcNow,
            authorName
        );

        await AddIncomingPainting(paintingMessage);
    }


    public async Task<List<Painting>> GetIncomingPaintings()
        => await _db.GetPaintings(false);

    public async Task<List<CreationPaintingMessage>> GetIncomingPaintingsMessages()
        => await ToPaintingMessages(await GetIncomingPaintings());

    public async Task<List<Painting>> GetAcceptedPaintings()
        => await _db.GetPaintings(true);

    public async Task<List<CreationPaintingMessage>> GetAcceptedPaintingsMessages()
        => await ToPaintingMessages(await GetAcceptedPaintings());


    public async Task<bool> AddIncomingPainting(CreationPaintingMessage painting)
        => await AddIncoming(painting,
            async p => await _db.GetPainting(p.Painting) != null,
            async p => await _db.AddPainting(p.Painting,
                p.Name,
                p.Description,
                p.Author,
                p.SenderUserId,
                p.CreationTime,
                false),
            (eui, p) => eui.SendNewIncomingPainting(p));

    public async void RemoveIncomingPainting(CreationPaintingMessage painting)
        => await ProcessIncoming(
            painting,
            async p => await _db.RemovePainting(p.Painting),
            (eui, p) => eui.SendRemoveIncomingPainting(p)
        );

    public async void RemoveAcceptedPainting(CreationPaintingMessage painting)
        => await ProcessIncoming(
            painting,
            async p => await _db.RemovePainting(p.Painting),
            (eui, p) => eui.SendRemoveAcceptedPainting(p)
        );


    public async void AcceptIncomingPainting(CreationPaintingMessage painting)
        => await ProcessIncoming(
            painting,
            async p => await _db.SetPaintingAccepted(p.Painting),
            (eui, p) =>
            {
                eui.SendRemoveIncomingPainting(p);
                eui.SendNewAcceptedPainting(p);
            });

    #endregion


    #region Books
    private async void OnSendBook(SendCreationBookEvent args)
    {
        if (!ValidateBookInput(args.Text, args.Name, args.Description))
            return;

        var book = new CreationBook(args.Text,
            args.Name,
            args.Description,
            args.Author,
            args.SenderPlayer,
            DateTime.UtcNow
        );

        await AddIncomingBook(book);

    }

    public async Task<List<Book>> GetIncomingBooks()
        => await _db.GetBooks(false);

    public async Task<List<CreationBook>> GetIncomingCreationBooks()
        => ToBookMessages(await GetIncomingBooks());

    public async Task<List<Book>> GetAcceptedBooks()
        => await _db.GetBooks(true);

    public async Task<List<CreationBook>> GetAcceptedCreationBooks()
        => ToBookMessages(await GetAcceptedBooks());


    public async Task<bool> AddIncomingBook(CreationBook book)
        => await AddIncoming(
            book,
            async b => await _db.GetBook(b.Text) != null,
            async b => await _db.AddBook(
                b.Text,
                b.Name,
                b.Description,
                b.Author,
                b.SenderUserId,
                b.CreationTime,
                false),
            (eui, b) => eui.SendNewIncomingBook(b)
        );


    public async void RemoveIncomingBook(CreationBook book)
        => await ProcessIncoming(
            book,
            async b => await _db.RemoveBook(b.Text),
            (eui, b) => eui.SendRemoveIncomingBook(b)
        );

    public async void RemoveAcceptedBook(CreationBook book)
        => await ProcessIncoming(
            book,
            async b => await _db.RemoveBook(b.Text),
            (eui, b) => eui.SendRemoveAcceptedBook(b)
        );


    public async void AcceptIncomingBook(CreationBook book)
        => await ProcessIncoming(
            book,
            async b => await _db.SetBookAccepted(b.Text),
            (eui, b) =>
            {
                eui.SendRemoveIncomingBook(b);
                eui.SendNewAcceptedBook(b);
            });

    #endregion

}

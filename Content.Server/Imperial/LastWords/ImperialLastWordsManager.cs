using Content.Shared.Imperial.LastWords;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.LastWords;

/// <summary>
/// Последние слова перед смертью. Хранит последнюю фразу каждого игрока; когда моб переходит в критическое
/// состояние и фраза сказана не раньше чем за <see cref="Window"/> до этого — фраза становится последними словами.
/// В конце раунда сервер сам рассылает собранные слова всем клиентам (<see cref="MsgImperialLastWords"/>).
/// Клиент запросить их не может — сообщение только сервер → клиент.
/// </summary>
public sealed class ImperialLastWordsManager
{
    [Dependency] private readonly IServerNetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>Сколько секунд до критического состояния фраза ещё считается последней.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(5);

    /// <summary>Последняя фраза каждого говорившего игрока и когда она сказана.</summary>
    private readonly Dictionary<EntityUid, (string Message, TimeSpan Time)> _recent = new();

    /// <summary>Зафиксированные последние слова.</summary>
    private readonly Dictionary<EntityUid, string> _lastWords = new();

    private bool _initialized;

    public IReadOnlyDictionary<EntityUid, string> LastWords => _lastWords;

    public void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;
        _net.RegisterNetMessage<MsgImperialLastWords>();
    }

    /// <summary>Игрок что-то сказал.</summary>
    public void OnSpoke(EntityUid uid, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        _recent[uid] = (message, _timing.CurTime);
    }

    /// <summary>
    /// Моб перешёл в критическое состояние: если он говорил последние <see cref="Window"/> — это его последние слова.
    /// </summary>
    public void OnCritical(EntityUid uid)
    {
        if (!_recent.TryGetValue(uid, out var recent) || _timing.CurTime - recent.Time > Window)
            return;

        _lastWords[uid] = recent.Message;
    }

    public void OnDeleted(EntityUid uid)
    {
        _recent.Remove(uid);
    }

    /// <summary>Разослать последние слова всем клиентам.</summary>
    public void Send(MsgImperialLastWords message)
    {
        _net.ServerSendToAll(message);
    }

    public void Clear()
    {
        _recent.Clear();
        _lastWords.Clear();
    }
}

using Content.Shared.GameTicking.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking;

// Imperial Space: фон лобби ивента Кадии. Отдельным файлом, чтобы не менять ванильный GameTicker.
public sealed partial class GameTicker
{
    private const string EventBackgroundPrefix = "CadiaLobby";

    /// <summary>
    /// Лобби показывает только эти фоны, в том числе после перезапуска раунда.
    /// Пустой список возвращает обычные фоны.
    /// </summary>
    public void SetEventLobbyBackgrounds(List<ProtoId<LobbyBackgroundPrototype>>? backgrounds)
    {
        _lobbyBackgrounds = null;
        if (backgrounds == null || backgrounds.Count == 0)
        {
            InitializeLobbyBackground();
            RemoveEventLobbyBackgrounds();
        }
        else
        {
            _lobbyBackgrounds = new List<ProtoId<LobbyBackgroundPrototype>>(backgrounds);
            RandomizeLobbyBackground();
        }
        SendStatusToAll();
    }

    /// <summary>Ванильная загрузка берёт все фоны подряд, а фоны Кадии нужны только при включённом оформлении.</summary>
    public void RemoveEventLobbyBackgrounds()
    {
        if (_lobbyBackgrounds == null || _lobbyBackgrounds.RemoveAll(b => b.Id.StartsWith(EventBackgroundPrefix)) == 0)
            return;
        RandomizeLobbyBackground();
    }
}

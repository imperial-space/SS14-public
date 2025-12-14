using Content.Server.Database;
using Content.Shared.Preferences;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Content.Shared.Imperial.Medieval.Flavors;
using System.IO;
using SixLabors.ImageSharp;
using Content.Server.Preferences.Managers;
using Robust.Shared.Utility;
using Content.Shared.Mind.Components;
using Robust.Server.Player;
using Content.Shared.DetailExaminable;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Imperial.ICCVar;

namespace Content.Server.Imperial.Medieval.Flavors
{
    public sealed class ServerFlavorManager : SharedFlavorManager, IPostInjectInit
    {
        [Dependency] private readonly IServerNetManager _netManager = default!;
        [Dependency] private readonly IServerDbManager _db = default!;
        [Dependency] private readonly IServerPreferencesManager _prefs = default!;
        [Dependency] private readonly UserDbDataManager _userDb = default!;
        [Dependency] private readonly IPlayerManager _players = default!;
        [Dependency] private readonly ISharedPlaytimeManager _playtime = default!;
        private Dictionary<NetUserId, Dictionary<string, byte[]>> _imageCache = new();

        public void Init()
        {
            _netManager.RegisterNetMessage<FlavorImagesMsg>();
            _netManager.RegisterNetMessage<UpdateFlavorCacheMsg>();
            _netManager.RegisterNetMessage<MsgUpdateFlavorImage>(UpdateImage);
            _netManager.RegisterNetMessage<OpenFlavorWindowMsg>();
        }
        void IPostInjectInit.PostInject()
        {
            _userDb.AddOnPlayerDisconnect(OnClientDisconnected);
        }
        public void UpdateImage(MsgUpdateFlavorImage msg)
        {
            if (!_players.TryGetSessionByChannel(msg.MsgChannel, out var player) || !_playtime.GetPlayTimes(player).TryGetValue(PlayTimeTrackingShared.TrackerOverall, out var time) || time < TimeSpan.FromSeconds(Config.GetCVar(ICCVars.FlavorPlaytimeRequirement)))
                return;

            using (var stream = new MemoryStream(msg.Image))
            {
                using (var img = Image.Load(stream))
                {
                    if (!ImageAllowed(img.Width, img.Height))
                        return;
                }
            }
            var prefs = _prefs.GetPreferencesOrNull(msg.MsgChannel.UserId);
            if (prefs == null)
                return;

            var slot = prefs.SelectedCharacterIndex;
            _db.AddOrUpdateFlavorImage(msg.MsgChannel.UserId, msg.Image, new(), slot);
            _netManager.ServerSendToAll(new UpdateFlavorCacheMsg() { CacheImages = new() { [$"{msg.MsgChannel.UserId}{slot}"] = msg.Image } });
        }
        public async void FinishLoad(ICommonSession session, MsgPreferencesAndSettings prefsMsg)
        {
            var images = new Dictionary<int, byte[]>();
            var playerCachedImages = new Dictionary<string, byte[]>();
            foreach (var (key, _) in prefsMsg.Preferences.Characters)
            {
                var image = await _db.GetFlavorImage(session.UserId.UserId, new(), key);
                if (image == null)
                    continue;

                images.Add(key, image.Image);
                playerCachedImages.Add($"{session.UserId}{key}", image.Image);
                _imageCache.GetOrNew(session.UserId)[$"{session.UserId}{key}"] = image.Image;
            }
            var msg = new FlavorImagesMsg();
            msg.PlayerImages = images;
            var toCache = new Dictionary<string, byte[]>();
            foreach (var (_, cache) in _imageCache)
            {
                foreach (var (key, value) in cache)
                {
                    toCache[key] = value;
                }
            }
            msg.CacheImages = toCache;
            _netManager.ServerSendMessage(msg, session.Channel);
            _netManager.ServerSendToAll(new UpdateFlavorCacheMsg() { CacheImages = playerCachedImages });
        }
        public void OnClientDisconnected(ICommonSession session)
        {
            _imageCache.Remove(session.UserId);
        }
        public override bool TryExamine(EntityUid user, Entity<DetailExaminableComponent> ent)
        {
            if (!EntityManager.TryGetComponent<FlavorImageComponent>(ent, out var imageComponent))
                return true;

            if (!_players.TryGetSessionByEntity(user, out var session))
                return true;

            var path = string.Empty;
            if (imageComponent.ImagePath != null)
                path = imageComponent.ImagePath;

            _netManager.ServerSendMessage(new OpenFlavorWindowMsg() { Description = ent.Comp.Content, Path = path }, session.Channel);
            return true;
        }
    }
}

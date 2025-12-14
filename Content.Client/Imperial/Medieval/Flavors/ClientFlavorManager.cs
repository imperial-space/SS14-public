using System.IO;
using System.Linq;
using Content.Shared.Imperial.Medieval.Flavors;
using Robust.Client;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Network;
using Robust.Shared.Utility;
using Robust.Shared.ContentPack;
using Content.Shared.DetailExaminable;

namespace Content.Client.Imperial.Medieval.Flavors
{
    public sealed class ClientFlavorManager : SharedFlavorManager
    {
        public const string FallbackFlavorImagePath = "/Textures/Imperial/Medieval/Flavors/flavors.rsi";
        public const string FallbackFlavorImageState = "black";
        public const string FlavorImagesFolderName = "Spellward/FlavorImages";
        [Dependency] private readonly IClientNetManager _netManager = default!;
        [Dependency] private readonly IBaseClient _baseClient = default!;
        [Dependency] private readonly IResourceCache _resources = default!;
        [Dependency] private readonly IDependencyCollection _collection = default!;
        [Dependency] private readonly ILogManager _log = default!;
        private ISawmill _sawmill = default!;
        private SpriteSystem _sprite = default!;
        public event Action? OnServerDataLoaded;
        public Dictionary<int, byte[]> Images { get; private set; } = new();

        public void Initialize()
        {
            _sawmill = _log.GetSawmill("client_flavor_manager");
            _netManager.RegisterNetMessage<FlavorImagesMsg>(ImagesReceived);
            _netManager.RegisterNetMessage<MsgUpdateFlavorImage>();
            _netManager.RegisterNetMessage<UpdateFlavorCacheMsg>(CacheUpdate);
            _netManager.RegisterNetMessage<OpenFlavorWindowMsg>(OpenFlavorWindow);

            _baseClient.RunLevelChanged += BaseClientOnRunLevelChanged;
        }
        public void Shutdown()
        {
            var folder = new ResPath($"/{FlavorImagesFolderName}");
            _resources.UserData.Delete(folder);
            _resources.UserData.CreateDir(folder);
        }
        private void CacheUpdate(UpdateFlavorCacheMsg msg)
        {
            foreach (var (path, image) in msg.CacheImages)
            {
                SetImage(path, image);
            }
        }

        private void BaseClientOnRunLevelChanged(object? sender, RunLevelChangedEventArgs e)
        {
            if (e.NewLevel == ClientRunLevel.Initialize)
            {
                Images = new();
            }
        }

        public void UpdateImage(int slot, byte[]? image)
        {
            if (image == null)
                image = Array.Empty<byte>();

            Images[slot] = image;
            var msg = new MsgUpdateFlavorImage
            {
                Slot = slot,
                Image = image
            };
            _netManager.ClientSendMessage(msg);
        }

        private void ImagesReceived(FlavorImagesMsg message)
        {
            Images = message.PlayerImages;
            _sawmill.Info("Received Images");
            foreach (var (path, image) in message.CacheImages)
            {
                SetImage(path, image);
                _sawmill.Info($"index: {path} value: {image.Count()}");
            }
            OnServerDataLoaded?.Invoke();
        }
        public (Texture texture, bool fallback) GetImageFromByteArray(byte[]? bytes)
        {
            if (_sprite == null)
                _sprite = EntityManager.System<SpriteSystem>();

            var fallback = _sprite.Frame0(new SpriteSpecifier.Rsi(new(FallbackFlavorImagePath), FallbackFlavorImageState));
            if (bytes == null || bytes.Count() == 0)
                return (fallback, true);

            Texture result;
            using (var stream = new MemoryStream(bytes))
            {
                result = Texture.LoadFromPNGStream(stream, ".webp");
            }
            if (!ImageAllowed(result.Width, result.Height))
                return (fallback, true);

            return (result, false);
        }
        public (Texture texture, bool fallback) GetImage(string fileName)
        {
            ResPath resPath = new(GetPathUsingFileName(fileName));

            byte[]? resultBytes = null;
            if (_resources.UserData.Exists(resPath))
                resultBytes = _resources.UserData.ReadAllBytes(resPath);

            return GetImageFromByteArray(resultBytes);
        }
        public void SetImage(string fileName, byte[] image)
        {
            _resources.UserData.WriteAllBytes(new(GetPathUsingFileName(fileName)), image);
        }
        public string GetPathUsingFileName(string fileName)
        {
            return $"/{FlavorImagesFolderName}/{fileName}.webp";
        }
        public void OpenFlavorWindow(OpenFlavorWindowMsg msg)
        {
            new FlavorExamineWindow(msg.Description, msg.Path).OpenCentered();
        }
        public override bool TryExamine(EntityUid user, Entity<DetailExaminableComponent> ent)
        {
            return true;
        }
    }
}

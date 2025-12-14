
using Content.Shared.Imperial.Medieval.Flavors;
using Content.Server.Preferences.Managers;
using Content.Shared.Mind.Components;
using Robust.Server.Player;
using Content.Shared.DetailExaminable;

namespace Content.Server.Imperial.Medieval.Flavors
{
    public sealed class ServerFlavorSystem : EntitySystem
    {
        [Dependency] private readonly IPlayerManager _players = default!;
        [Dependency] private readonly IServerPreferencesManager _prefs = default!;
        [Dependency] private readonly IEntityManager _ent = default!;

        public override void Initialize()
        {
            _ent.EventBus.SubscribeLocalEvent<FlavorImageComponent, ComponentInit>(CompInit);
            _ent.EventBus.SubscribeLocalEvent<FlavorImageComponent, MindAddedMessage>(MindAdded);
        }
        public void CompInit(EntityUid uid, FlavorImageComponent component, ComponentInit args)
        {
            MindAdded(uid, component, new(new(), new()));
        }
        public void MindAdded(EntityUid uid, FlavorImageComponent component, MindAddedMessage args)
        {
            if (component.ImagePath != null)
                return;

            if (!_players.TryGetSessionByEntity(uid, out var session))
                return;

            var prefs = _prefs.GetPreferencesOrNull(session.UserId);
            if (prefs == null)
                return;

            component.ImagePath = $"{session.UserId}{prefs.SelectedCharacterIndex}";
        }
    }
}

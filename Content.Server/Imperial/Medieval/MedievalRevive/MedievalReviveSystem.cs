using System.Linq;
using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Ghost.Components;
using Content.Server.Mind;
using Content.Server.Roles.Jobs;
using Content.Shared.Actions;
using Content.Shared.CCVar;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Eye;
using Content.Shared.FixedPoint;
using Content.Shared.Follower;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Medieval.CCVar;
using Content.Shared.Imperial.Medieval.Revive;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.NameModifier.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Tag;
using Content.Shared.Warps;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Revive
{
    public sealed class MedievalReviveSystem : EntitySystem
    {
        [Dependency] private readonly MindSystem _minds = default!;
        [Dependency] private readonly TransformSystem _transformSystem = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly IConfigurationManager _cfg = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeNetworkEvent<GhostReviveRequestEvent>(OnGhostReviveRequest);
        }
        private void OnGhostReviveRequest(GhostReviveRequestEvent msg, EntitySessionEventArgs args)
        {
            var revivesOn = _cfg.GetCVar(MedievalCCVars.GhostRevive);
            if (!revivesOn) return;

            var player = args.SenderSession;
            var reviveQuery = EntityManager.EntityQuery<MedievalReviveSpawnerComponent>();

            if (reviveQuery.Count() == 0) return;

            var reviveList = reviveQuery.ToList();

            _random.Shuffle<MedievalReviveSpawnerComponent>(reviveList);
            var component = reviveList.ElementAt(0);
            var spawner = component.Owner;

            var mob = Spawn(component.Prototype, Transform(spawner).Coordinates);
            _transformSystem.AttachToGridOrMap(mob);

            EnsureComp<MindContainerComponent>(mob);

            if (_minds.TryGetMind(player.UserId, out _, out var mind) && !mind.IsVisitingEntity)
                _minds.WipeMind(player);

            var newMind = _minds.CreateMind(player.UserId,
                Comp<MetaDataComponent>(mob).EntityName);

            _minds.SetUserId(newMind, player.UserId);
            _minds.TransferTo(newMind, mob);
        }
    }
}

using Content.Shared.Siege.Components;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Content.Server.Administration;
using Content.Shared.Verbs;
using Content.Shared.DoAfter;
using Robust.Shared.Utility;
using Content.Shared.Siege.Events;
using Content.Server.Prayer;
using System.Numerics;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;
using Content.Server.ShiftFront.Components;
using Content.Shared.ShiftFront.Components;
using Content.Shared.Speech;
using Content.Server.Chat.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.ShiftFront;
using Content.Shared.Projectiles;
using Content.Shared.Movement.Systems;
using System.Linq;
using System.Collections.Generic;
using Robust.Shared.Physics.Components;
using Content.Shared.Mind;
using Content.Server.Mind;
using Content.Shared.Mind.Components;
using Content.Server.Ghost;
using Robust.Shared.Prototypes;
using Content.Shared.ShiftFrontResearch;

namespace Content.Server.ShiftFront
{
    public sealed partial class ShiftResearchSystem : EntitySystem
    {
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
        [Dependency] private readonly ISharedPlayerManager _sharedPlayerManager = default!;
        [Dependency] private readonly PrayerSystem _prayerSystem = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly ChatSystem _chat = default!;
        [Dependency] private readonly DamageableSystem _damageableSystem = default!;
        [Dependency] private readonly UserInterfaceSystem _ui = default!;
        [Dependency] private readonly SharedContentEyeSystem _eye = default!;
        [Dependency] private readonly MindSystem _minds = default!;
        [Dependency] private readonly GhostSystem _ghost = default!;
        [Dependency] protected readonly SharedAudioSystem Audio = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<ShiftConsoleResearchComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
            SubscribeLocalEvent<ShiftConsoleResearchComponent, ExaminedEvent>(OnExamine);
        }
        private void OnExamine(EntityUid uid, ShiftConsoleResearchComponent comp, ExaminedEvent args)
        {
            args.PushMarkup($"Очки исследований - [color=cyan]{comp.Points}[/color]", 15);
            args.PushMarkup("Изучено:", 14);
            foreach (var i in comp.Researched)
                args.PushMarkup($"  [color=green]{i}[/color]", 13);
        }
        public bool TryResearch(string name, ShiftConsoleResearchComponent comp, int price, ICommonSession session, string id)
        {
            if (price > comp.Points)
            {
                _prayerSystem.SendSubtleMessage(session, session, $"Для исследования {name} требуется {price} очков исследования, у вас только {comp.Points}", "Недостаточно очков");
                return false;
            }
            _prayerSystem.SendSubtleMessage(session, session, $"Исследование {name} было изучено, потрачено {price} очков исследования", "Исследование успешно");
            comp.Points -= price;
            comp.Researched.Add(id);
            if (id == "ShiftFrontPsycho")
            {
                var spquery = EntityQueryEnumerator<ShiftPlayerComponent>();
                while (spquery.MoveNext(out var suid, out var scomp))
                {
                    if (scomp.Faction == comp.Faction)
                        scomp.SuppressionMax += 10;
                }
            }
            if (id == "ShiftFrontPsycho2")
            {
                var spquery = EntityQueryEnumerator<ShiftPlayerComponent>();
                while (spquery.MoveNext(out var suid, out var scomp))
                {
                    if (scomp.Faction == comp.Faction)
                        scomp.SuppressionMax += 15;
                }
            }
            if (id == "ShiftFrontClonSpeedUp")
            {
                var spquery = EntityQueryEnumerator<ShiftBarracksComponent>();
                while (spquery.MoveNext(out var suid, out var scomp))
                {
                    if (scomp.Faction == comp.Faction)
                        scomp.Boost += 7;
                }
            }
            if (id == "ShiftFrontClonSpeedUp2")
            {
                var spquery = EntityQueryEnumerator<ShiftBarracksComponent>();
                while (spquery.MoveNext(out var suid, out var scomp))
                {
                    if (scomp.Faction == comp.Faction)
                        scomp.Boost += 7;
                }
            }
            if (id == "ShiftFrontFactorySpeedUp")
            {
                var spquery = EntityQueryEnumerator<ShiftSuppliesComponent>();
                while (spquery.MoveNext(out var suid, out var scomp))
                {
                    if (scomp.Faction == comp.Faction)
                        scomp.OverallGenTime -= 10;
                }
            }
            if (id == "ShiftFrontFactorySpeedUp2")
            {
                var spquery = EntityQueryEnumerator<ShiftSuppliesComponent>();
                while (spquery.MoveNext(out var suid, out var scomp))
                {
                    if (scomp.Faction == comp.Faction)
                        scomp.OverallGenTime -= 15;
                }
            }
            return true;
        }
        private void OnGetAlternativeVerbs(EntityUid uid, ShiftConsoleResearchComponent comp, GetVerbsEvent<AlternativeVerb> ev)
        {
            if (!ev.CanAccess || !ev.CanInteract) return;
            if (!_sharedPlayerManager.TryGetSessionByEntity(ev.User, out var session)) return;
            if (TryComp<ShiftPlayerComponent>(ev.User, out var shiftPlayer) && !shiftPlayer.Leader) return;

            var researches = _prototypeManager.EnumeratePrototypes<ShiftFrontResearchPrototype>();
            foreach (var research in researches)
            {
                if (comp.Researched == null || !comp.Researched.Contains(research.ID))
                {
                    bool canResearch = true;
                    foreach (var needresearch in research.RequiredResearches)
                    {
                        if (comp.Researched == null || !comp.Researched.Contains(needresearch))
                        {
                            canResearch = false;
                            break;
                        }
                    }
                    foreach (var banresearch in research.BannedResearches)
                    {
                        if (comp.Researched != null && comp.Researched.Contains(banresearch))
                        {
                            canResearch = false;
                            break;
                        }
                    }
                    if (research.UnicForFaction != "" && comp.Faction != research.UnicForFaction) canResearch = false;
                    if (!canResearch) continue;

                    switch (research.Tier)
                    {
                        case 0:
                            ev.Verbs.Add(new AlternativeVerb
                            {
                                Act = () =>
                                {
                                    TryResearch(research.ResearchName, comp, research.Price, session, research.ID);
                                },
                                Category = VerbCategory.ShiftFrontResearchT0,
                                Text = research.ResearchName
                            });
                            break;
                        case 1:
                            ev.Verbs.Add(new AlternativeVerb
                            {
                                Act = () =>
                                {
                                    TryResearch(research.ResearchName, comp, research.Price, session, research.ID);
                                },
                                Category = VerbCategory.ShiftFrontResearchT1,
                                Text = research.ResearchName
                            });
                            break;
                        case 2:
                            ev.Verbs.Add(new AlternativeVerb
                            {
                                Act = () =>
                                {
                                    TryResearch(research.ResearchName, comp, research.Price, session, research.ID);
                                },
                                Category = VerbCategory.ShiftFrontResearchT2,
                                Text = research.ResearchName
                            });
                            break;
                        case 3:
                            ev.Verbs.Add(new AlternativeVerb
                            {
                                Act = () =>
                                {
                                    TryResearch(research.ResearchName, comp, research.Price, session, research.ID);
                                },
                                Category = VerbCategory.ShiftFrontResearchT3,
                                Text = research.ResearchName
                            });
                            break;
                    }
                }
            }
        }
        public TimeSpan StartTime = TimeSpan.FromSeconds(0f);
        public TimeSpan EndTime = TimeSpan.FromSeconds(0f);
        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            if (_timing.CurTime > EndTime)
            {
                StartTime = _timing.CurTime;
                EndTime = StartTime + TimeSpan.FromSeconds(1f);

                var exquery = EntityQueryEnumerator<ShiftResearchGenerateComponent>();
                while (exquery.MoveNext(out var uid, out var comp))
                {
                    var redquery = EntityQueryEnumerator<ShiftConsoleResearchComponent>();
                    while (redquery.MoveNext(out var reuid, out var recomp))
                    {
                        if (recomp.Faction != comp.Faction) continue;
                        if (comp.TimeTillNextGen > 0)
                            comp.TimeTillNextGen -= 1;
                        else
                        {
                            comp.TimeTillNextGen = comp.OverallGenTime;
                            recomp.Points += comp.Points;
                        }
                    }
                }
            }
        }
    }
}

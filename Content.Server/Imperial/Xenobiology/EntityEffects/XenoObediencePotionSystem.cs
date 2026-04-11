using Content.Server.Popups;
using Content.Shared.EntityEffects;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.Imperial.Xenobiology.EntityEffects;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Robust.Server.GameObjects;

namespace Content.Server.Imperial.Xenobiology.EntityEffects;

/// <summary>
/// Зелье послушания: находит слаймов рядом и добавляет пользователя (и других мобов рядом)
/// в их список друзей. Если эффект применён к самому слайму — он становится дружелюбным.
/// </summary>
public sealed partial class XenoObediencePotionSystem
    : EntityEffectSystem<MetaDataComponent, XenoObediencePotionEffect>
{
    [Dependency] private readonly EntityLookupSystem _lookup     = default!;
    [Dependency] private readonly NpcFactionSystem   _factionSys = default!;
    [Dependency] private readonly PopupSystem        _popup      = default!;

    private const string FactionNeutral = "XenoSlimeFaction";
    private const string FactionAggressive = "SimpleHostile";

    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<XenoObediencePotionEffect> args)
    {
        var uid = entity.Owner;
        var range = args.Effect.Range;

        // Если само зелье метаболизируется слаймом — делаем его нейтральным
        if (TryComp<XenoSlimeComponent>(uid, out var slime))
        {
            MakeSlimeFriendlyToAll(uid, slime, args.User);
            _popup.PopupEntity(
                Loc.GetString("xeno-obedience-potion-success", ("count", 1)),
                uid,
                PopupType.Medium);
            return;
        }

        // Иначе: ищем ближайших слаймов и добавляем пользователя в их друзья
        var slimes = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(uid, range, slimes);

        var friendCount = 0;
        foreach (var slimeUid in slimes)
        {
            if (!TryComp<XenoSlimeComponent>(slimeUid, out var slimeComp))
                continue;

            MakeSlimeFriendlyToAll(slimeUid, slimeComp, args.User);
            friendCount++;
        }

        if (friendCount > 0)
        {
            _popup.PopupEntity(
                Loc.GetString("xeno-obedience-potion-success", ("count", friendCount)),
                uid, PopupType.Medium);
        }
    }

    /// <summary>
    /// Делает слайм нейтральным и добавляет указанную сущность (и другие пустые слоты) в его список друзей.
    /// </summary>
    public void MakeSlimeFriendlyToAll(EntityUid slimeUid, XenoSlimeComponent slimeComp, EntityUid? friend)
    {
        // Переводим в нейтральный режим
        if (slimeComp.Mood != XenoSlimeMood.Neutral)
        {
            slimeComp.Mood = XenoSlimeMood.Neutral;
            _factionSys.RemoveFaction(slimeUid, FactionAggressive, dirty: true);
            _factionSys.AddFaction(slimeUid, FactionNeutral, dirty: true);
        }

        // Сбрасываем голод чтобы слайм не сразу снова стал агрессивным
        slimeComp.HungerPercent = 50f;

        // Добавляем друга
        if (friend.HasValue)
        {
            slimeComp.FeedCounts.TryGetValue(friend.Value, out var cnt);
            slimeComp.FeedCounts[friend.Value] = Math.Max(cnt, 2);
            slimeComp.Friends.Add(friend.Value);
        }
    }
}

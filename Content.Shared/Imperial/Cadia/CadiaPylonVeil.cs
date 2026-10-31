using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Завеса пилонов. Пока идёт подготовка обороны, сеть пилонов Кадии работает в полную силу: её поле глушит варп
/// над Касром и закрывает небо. Флот Хаоса не может спуститься с орбиты, корабли и батареи не могут стрелять, а оружие
/// на поле молчит — бой начинается с обстрелом, когда флот Абаддона перегружает завесу.
/// Общая для клиента и сервера, чтобы отказ выстрела предсказывался и пули не появлялись у стрелка зря.
/// </summary>
public sealed class CadiaPylonVeilSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly TimeSpan PopupDelay = TimeSpan.FromSeconds(2);
    private readonly Dictionary<EntityUid, TimeSpan> _nextPopup = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CadiaFactionMemberComponent, ShotAttemptedEvent>(OnShot);
        SubscribeLocalEvent<CadiaFactionMemberComponent, AttackAttemptEvent>(OnAttack);
    }

    /// <summary>Держится ли завеса: идёт война и она ещё в подготовке.</summary>
    public bool Active()
    {
        var query = EntityQueryEnumerator<CadiaWarStateComponent>();
        while (query.MoveNext(out var war))
        {
            if (VeilUp(war))
                return true;
        }
        return false;
    }

    /// <summary>Завеса над этой войной: по решению ведущего или, без него, пока идёт подготовка.</summary>
    public static bool VeilUp(CadiaWarStateComponent war)
    {
        return war.VeilOverride ?? war.Phase == CadiaWarPhase.Preparation;
    }

    /// <summary>
    /// Завеса не даёт этого сделать: сообщает пользователю (не чаще раза в пару секунд) и возвращает true.
    /// predicted — проверка идёт и на клиенте; для чисто серверных действий false.
    /// </summary>
    public bool Blocks(EntityUid user, string message = "cadia-veil-weapons", bool predicted = true)
    {
        if (!Active())
            return false;
        var now = _timing.CurTime;
        if (!_nextPopup.TryGetValue(user, out var next) || now >= next)
        {
            _nextPopup[user] = now + PopupDelay;
            // Выстрел предсказывается клиентом — ему и показываем; серверные действия сообщают сами.
            if (predicted)
                _popup.PopupClient(Loc.GetString(message), user, user, PopupType.MediumCaution);
            else
                _popup.PopupEntity(Loc.GetString(message), user, user, PopupType.MediumCaution);
        }
        return true;
    }

    private void OnShot(Entity<CadiaFactionMemberComponent> ent, ref ShotAttemptedEvent args)
    {
        if (Blocks(ent))
            args.Cancel();
    }

    private void OnAttack(EntityUid uid, CadiaFactionMemberComponent comp, AttackAttemptEvent args)
    {
        if (Blocks(uid))
            args.Cancel();
    }
}

/// <summary>Завеса пала: клиент вспыхивает небом красного варпа.</summary>
[Robust.Shared.Serialization.NetSerializable, Serializable]
public sealed class CadiaVeilFallEvent : EntityEventArgs;

/// <summary>Ритуал над завесой: колдуны Хаоса раскачивают её, жрецы Империума укрепляют у пилонов.</summary>
[Robust.Shared.Serialization.NetSerializable, Serializable]
public sealed partial class CadiaVeilRitualDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;

/// <summary>
/// Буря посреди поля на время подготовки: стена снега между пилонами (за Империумом) и пустошью высадки (за Хаосом).
/// Пройти сквозь неё нельзя; с началом обстрела, когда завеса падает, буря рассеивается.
/// </summary>
public static class CadiaMidStorm
{
    /// <summary>Клетка-столбец бури: мировая X левого края (поле 240×150, начало в центре; пилоны до X = 38, высадка с X = 64).</summary>
    public const int Column = 55;
    /// <summary>Как широко буря видна по обе стороны столбца.</summary>
    public const float VisualHalfWidth = 7f;
}

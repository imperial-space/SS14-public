using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Lavaland.Artifact;
using Robust.Shared.GameObjects;

namespace Content.Server.Imperial.Lavaland.Artifact;

/// <summary>
/// Система обработки эффектов темного слугу
/// </summary>
public sealed class DarkServantSystem : EntitySystem
{

    private float _tickTimer = 0f;
    private const float TickRate = 1f; // проверяем каждую секунду

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        
        _tickTimer += frameTime;
        if (_tickTimer < TickRate)
            return;

        _tickTimer -= TickRate;

        // Обрабатываем всех темных слуг
        var query = AllEntityQuery<DarkServantComponent>();
        while (query.MoveNext(out var uid, out var servant))
        {
            UpdateDarkServant(uid, servant);
        }
    }

    /// <summary>
    /// Обновляет состояние темного слугу (проверяет свет/темноту)
    /// </summary>
    private void UpdateDarkServant(EntityUid uid, DarkServantComponent servant)
    {
        // TODO: Получить текущее освещение вокруг слугу
        // Если светло - наносить урон
        // Если темно - восстанавливать HP
        
        var damageSpec = new DamageSpecifier();
        
        // Временно: наносим легкий урон свету
        // damageSpec.DamageDict["Heat"] = servant.LightDamagePerSecond;
        // _damageableSystem.TryChangeDamage(uid, damageSpec);
    }
}

using Content.Shared.Damage;
using Robust.Shared.Random;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Presets;

/// <summary>
/// Создаёт новую спецификацию урона и масштабирует её до настроенного случайного итога.
/// Данные прототипа остаются неизменными, а пропорции типов урона сохраняются.
/// </summary>
public static class DeathNoteDamageHelper
{
    public static bool TryCreate(
        Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes.DeathNotePresetParameters parameters,
        IRobustRandom random,
        out DamageSpecifier damage)
    {
        return TryCreate(
            parameters.Damage,
            parameters.RandomDamageMin,
            parameters.RandomDamageMax,
            random,
            out damage);
    }

    public static bool TryCreate(
        DamageSpecifier configuredDamage,
        float minimum,
        float maximum,
        IRobustRandom random,
        out DamageSpecifier damage)
    {
        damage = new DamageSpecifier(configuredDamage);
        if (damage.Empty || minimum <= 0f || maximum < minimum)
            return false;

        var amount = random.NextFloat(minimum, maximum);
        var total = damage.GetTotal().Float();
        if (total <= 0f)
            return false;

        damage *= amount / total;
        return true;
    }

    public static bool HasValidRandomRange(
        Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes.DeathNotePresetParameters parameters)
    {
        return parameters.RandomDamageMin > 0f &&
               parameters.RandomDamageMax >= parameters.RandomDamageMin;
    }

    public static bool HasValidDamageAndRange(
        Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes.DeathNotePresetParameters parameters)
    {
        return !parameters.Damage.Empty &&
               parameters.Damage.GetTotal().Float() > 0f &&
               HasValidRandomRange(parameters);
    }

}

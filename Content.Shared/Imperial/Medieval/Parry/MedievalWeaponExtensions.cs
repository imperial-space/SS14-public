using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.MeleeParry;

public static class MedievalWeaponExtensions
{
    public static ParryParameters GetParryData(this MedievalWeaponSkillId skill)
    {
        return skill switch
        {
            // Булавы
            MedievalWeaponSkillId.OneHandedBlunt => new ParryParameters(1.25f, 0.75f),

            // Мечи (Сабля, Меч, Фальшион)
            MedievalWeaponSkillId.OneHandedSlashLarge => new ParryParameters(1f, 1),

            // Коротышы (Короткий меч, кинжалы)
            MedievalWeaponSkillId.OneHandedSlashSmall => new ParryParameters(0.65f, 0.7f),
            MedievalWeaponSkillId.OneHandedBluntLight => new ParryParameters(0.65f, 0.7f),

            // Двуручки (Цвайхер, Клеймор)
            MedievalWeaponSkillId.TwoHanded => new ParryParameters(1.25f, 0.5f),

            // Копья
            MedievalWeaponSkillId.Spear => new ParryParameters(1.4f, 0.85f),

            // Дефолт для щитов или безоружного боя
            MedievalWeaponSkillId.Shield => new ParryParameters(0f, 0f),
            _ => new ParryParameters(0f, 0f)
        };
    }
}

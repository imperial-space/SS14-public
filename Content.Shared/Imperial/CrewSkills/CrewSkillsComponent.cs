namespace Content.Shared.Imperial.CrewSkills
{
    [RegisterComponent]
    public sealed partial class CrewSkillsComponent : Component
    {
        [DataField("skills")] //Основные навыки
        public List<string> Skills = new();

        // [DataField("savedSkills")]
        // 
        // public List<string> SavedSkills = new();
        [DataField("isAntag")] //Антаг ли это?
        public bool IsAntag = false;
        [DataField("antagSkills")] //Навыки антага, если это антаг, то использоваться будут они, эти навыки не отображаются при осмотре
        public List<string> AntagSKills = new();
        [DataField("skillGroupStandartSS14All")]
        public bool CanReedBooks = true; //Может ли персонаж читать книги

        [ViewVariables(VVAccess.ReadWrite), DataField("cantReedBookMessage")]
        public string CantReedBookMessage = ""; //Сообщение если не может прочитать книгу

        [ViewVariables(VVAccess.ReadWrite), DataField("cantTeachBookMessage")]
        public string CantTeachBookMessage = ""; //Сообщение если не может учить кого то
        [ViewVariables(VVAccess.ReadWrite), DataField("cantReciveTeachBookMessage")]
        public string CantReciveTeachBookMessage = ""; // Сообщение, если не может обучиться у кого то
        [ViewVariables(VVAccess.ReadWrite), DataField("skillPoints")]
        public int SkillPoints = 0; //Количество очков навыков

    }

    public enum CrewSkillsManualCanReadKeys : byte
    {
        Read,
        TeachTarget,
        TeachMe
    }
}

/*
Список id стандартных навыков
- skillBotany #base
- skillBaseMed
- skillBaseConstraction
- skillCooking  #Easy
- skillBartending
- skillHacking
- skillInstrumentation #Medium
- skillMaintenance
- skillMediumConstruction
- skillMediumBotany
- skillBasicAtmos
- skillMedicalEquipment
- skillPharmacology
- skillPiloting #Hard
- skillAtmos
- skillHardInstrumentation
- skillRobotics
- skillResearch
- skillPharmaceuticals
- skillShooting
- skillSurgery
- skillNeuroSurgery
*/
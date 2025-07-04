namespace Content.Server.Imperial.Medieval.BloodRegenBed
{
    [RegisterComponent]
    public sealed partial class BloodRegenBedComponent : Component
    {
        [DataField("bloodRegenMultiplier", required: true)]
        public float BloodRegenMultiplier = 10.0f; // Добавляем 10 единиц крови каждые 5 секунд
    }
}

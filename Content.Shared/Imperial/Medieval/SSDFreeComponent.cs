using Robust.Shared.Player;
using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared.SSDFree.Components
{
    [RegisterComponent, NetworkedComponent]
    public sealed partial class SSDFreeComponent : Component
    {
        public DamageSpecifier SpikeDamage = new()
        {
            DamageDict = new()
            {
                { "Poison", 350 },
                { "Blunt", 99950 },
            }
        };

        [DataField]
        public TimeSpan StartTime = TimeSpan.FromSeconds(0f);

        [DataField]
        public TimeSpan EndTime = TimeSpan.FromSeconds(0f);

        [DataField]
        public TimeSpan ReloadTime = TimeSpan.FromSeconds(60f);
        [DataField]
        public TimeSpan StartTimeSes = TimeSpan.FromSeconds(0f);

        [DataField]
        public TimeSpan EndTimeSes = TimeSpan.FromSeconds(0f);

        [DataField]
        public TimeSpan ReloadTimeSes = TimeSpan.FromSeconds(10f);

        [DataField]
        public bool Checked = false;
        [DataField]
        public float UnholyValue = 0f;
        [DataField]
        public float UnholySpeed = 1f;
        [DataField]
        public float UnholyMaxValue = 10f;
        [DataField]
        public bool Enabled = true;
        [DataField]
        public bool GoSkeleton = true;
        [DataField]
        public bool DragonEaten = false;
        [DataField]
        public ICommonSession? CommonSession = null;
        [DataField]
        public EntityUid? Body = null;
    }
}

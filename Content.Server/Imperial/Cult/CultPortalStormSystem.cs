using Content.Server.Chat.Systems;
using Content.Server.Imperial.Cult.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cult;

/// <summary>
/// round_event/portal_storm/portal_storm_narsie: из порталов по станции выходят враждебные конструкты.
/// Тик события — 2 секунды, старт на 7-м тике.
/// </summary>
public sealed class CultPortalStormSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ImperialCultRuleSystem _rule = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private static readonly SoundSpecifier Lightning = new SoundPathSpecifier(CultSounds.Root + "lightningbolt.ogg");
    private static readonly EntProtoId Boss = "MobCultArtificerHostile";

    private sealed class Storm
    {
        public int ActiveFor;
        public TimeSpan NextTick;
        public readonly Dictionary<EntProtoId, int> Hostiles = new() { { "MobCultJuggernautHostile", 8 }, { "MobCultWraithHostile", 6 } };
        public int Bosses = 6;
        public int NextBossSpawn;
        public readonly List<EntityCoordinates> HostileSpawns = new();
        public readonly List<EntityCoordinates> BossSpawns = new();
    }

    private readonly List<Storm> _storms = new();

    public void StartStorm()
    {
        var storm = new Storm { NextTick = _timing.CurTime + TimeSpan.FromSeconds(2) };
        for (var i = 0; i < 14; i++)
        {
            if (_rule.TryRandomStationTile(out var c))
                storm.HostileSpawns.Add(c);
        }
        for (var i = 0; i < storm.Bosses; i++)
        {
            if (_rule.TryRandomStationTile(out var c))
                storm.BossSpawns.Add(c);
        }
        // start_when + ceil(2 * hostiles / bosses)
        storm.NextBossSpawn = 7 + (int) MathF.Ceiling(2f * 14 / 6);
        _storms.Add(storm);

        // announce()
        _audio.PlayGlobal(CultSounds.LightningChargeup, Filter.Broadcast(), true);
        Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(8), () =>
        {
            _chat.DispatchGlobalAnnouncement(Loc.GetString("cult-portal-storm-announcement"), playSound: true);
            Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(2), () => _audio.PlayGlobal(Lightning, Filter.Broadcast(), true));
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        for (var i = _storms.Count - 1; i >= 0; i--)
        {
            var storm = _storms[i];
            if (now < storm.NextTick)
                continue;
            storm.NextTick = now + TimeSpan.FromSeconds(2);
            storm.ActiveFor++;
            if (storm.ActiveFor < 7)
                continue;

            // tick()
            if (_rule.TryRandomStationTile(out var fx))
                SpawnEffects(fx);

            if (storm.ActiveFor % 2 == 0 && storm.Hostiles.Count > 0)
            {
                var type = _random.Pick(new List<EntProtoId>(storm.Hostiles.Keys));
                storm.Hostiles[type]--;
                if (storm.Hostiles[type] <= 0)
                    storm.Hostiles.Remove(type);
                SpawnMob(type, storm.HostileSpawns);
            }

            if (storm.Bosses > 0 && storm.ActiveFor == storm.NextBossSpawn)
            {
                storm.NextBossSpawn += (int) MathF.Ceiling(14f / 6);
                storm.Bosses--;
                SpawnMob(Boss, storm.BossSpawns);
            }

            if (storm.Hostiles.Count == 0 && storm.Bosses <= 0)
                _storms.RemoveAt(i);
        }
    }

    private void SpawnMob(EntProtoId type, List<EntityCoordinates> spawns)
    {
        if (spawns.Count == 0)
            return;
        var at = _random.PickAndTake(spawns);
        Spawn(type, at);
        SpawnEffects(at);
    }

    private void SpawnEffects(EntityCoordinates at)
    {
        Spawn("CultPortalStormEffect", at);
        _audio.PlayPvs(Lightning, at, AudioParams.Default.WithVolume(CultSystem.Db(_random.Next(80, 101))).WithVariation(0.05f));
    }
}

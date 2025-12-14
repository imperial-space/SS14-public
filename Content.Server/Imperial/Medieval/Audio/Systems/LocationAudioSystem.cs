using System;
using Content.Shared.Imperial.Medieval.Audio.Events;
using Robust.Shared.Audio;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Audio;

/// <summary>
/// Серверная система для отправки событий клиенту о локационных звуках
/// </summary>
public sealed class LocationAudioSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LocationTriggerComponent, StartCollideEvent>(OnTriggerEnter);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
    }

    /// <summary>
    /// Обрабатывает отключение игрока от сущности
    /// </summary>
    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        if (!TryComp<PlayerLocationComponent>(ev.Entity, out var playerComp))
            return;

        var stopEv = new StopAllLocationAudioEvent();
        RaiseNetworkEvent(stopEv, ev.Entity);
    }

    /// <summary>
    /// Обрабатывает подключение игрока к новой сущности
    /// </summary>
    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (!TryComp<PlayerLocationComponent>(ev.Entity, out var playerComp))
            return;

        // Сбрасываем состояние при подключении к новой сущности
        playerComp.CurrentLocationId = string.Empty;
        playerComp.PreviousLocationId = string.Empty;
        playerComp.RandomSoundVersion++;
        playerComp.CurrentFadeVersion++;
        playerComp.PreviousFadeVersion++;

        var stopEv = new StopAllLocationAudioEvent();
        RaiseNetworkEvent(stopEv, ev.Entity);
    }

    /// <summary>
    /// Обрабатывает вход игрока в зону триггера
    /// </summary>
    private void OnTriggerEnter(EntityUid uid, LocationTriggerComponent component, ref StartCollideEvent args)
    {
        if (args.OurFixtureId != component.FixtureId)
            return;

        if (!TryComp<PlayerLocationComponent>(args.OtherEntity, out var playerComp))
            return;

        if (playerComp.CurrentLocationId == component.LocationId)
            return;

        // Сохраняем предыдущую локацию
        var previousLocationId = playerComp.CurrentLocationId;

        // Обновляем информацию о локации
        playerComp.PreviousLocationId = playerComp.CurrentLocationId;
        playerComp.CurrentLocationId = component.LocationId;
        playerComp.RandomSoundVersion++;

        // Если игрок переходит в новую локацию (не пустую), останавливаем все звуки от предыдущей
        if (!string.IsNullOrEmpty(component.LocationId) && !string.IsNullOrEmpty(previousLocationId))
        {
            // Отправляем событие клиенту для остановки всех звуков от предыдущей локации
            var stopEv = new StopAllLocationAudioEvent();
            RaiseNetworkEvent(stopEv, args.OtherEntity);
        }

        // Отправляем событие клиенту для воспроизведения основного звука локации
        if (!string.IsNullOrEmpty(component.LocationId) && component.Sound != null)
        {
            string soundPath = component.Sound switch
            {
                SoundPathSpecifier path => path.Path.ToString(),
                _ => component.Sound.ToString() ?? string.Empty
            };

            if (!string.IsNullOrEmpty(soundPath))
            {
                var ev = new PlayLocationSoundEvent(soundPath, true);
                RaiseNetworkEvent(ev, args.OtherEntity);
            }
        }

        // Запускаем таймер случайных звуков
        StartRandomSoundTimer(args.OtherEntity, playerComp, component);
    }

    /// <summary>
    /// Запускает таймер для случайного звука
    /// </summary>
    private void StartRandomSoundTimer(EntityUid player, PlayerLocationComponent comp, LocationTriggerComponent trigger)
    {
        if (trigger.RandomSounds.Count == 0)
            return;

        comp.RandomSoundVersion++;
        var version = comp.RandomSoundVersion;

        void PlayRandomSound()
        {
            if (!EntityManager.EntityExists(player))
                return;

            if (!TryComp(player, out PlayerLocationComponent? c) || c != comp)
                return;

            if (version != comp.RandomSoundVersion)
                return;

            if (comp.CurrentLocationId != trigger.LocationId)
                return;

            var randomSound = _random.Pick(trigger.RandomSounds);

            // Отправляем событие клиенту для воспроизведения случайного звука
            string soundPath = randomSound switch
            {
                SoundPathSpecifier path => path.Path.ToString(),
                _ => randomSound.ToString() ?? string.Empty
            };

            if (!string.IsNullOrEmpty(soundPath))
            {
                var ev = new PlayRandomLocationSoundEvent(soundPath);
                RaiseNetworkEvent(ev, player);
            }

            // Устанавливаем следующий таймер
            var minInterval = TimeSpan.FromSeconds(trigger.MinRandomIntervalSeconds);
            var maxInterval = TimeSpan.FromSeconds(trigger.MaxRandomIntervalSeconds);
            var randomInterval = _random.Next(minInterval, maxInterval);

            Timer.Spawn(randomInterval, PlayRandomSound);
        }

        // Устанавливаем первый таймер
        var firstMinInterval = TimeSpan.FromSeconds(trigger.MinRandomIntervalSeconds);
        var firstMaxInterval = TimeSpan.FromSeconds(trigger.MaxRandomIntervalSeconds);
        var firstRandomInterval = _random.Next(firstMinInterval, firstMaxInterval);

        Timer.Spawn(firstRandomInterval, PlayRandomSound);
    }
}

game-ticker-restart-round = Перезапуск смены...
game-ticker-start-round = Смена начинается...
game-ticker-start-round-cannot-start-game-mode-fallback = Не удалось запустить режим {$failedGameMode}! Запускаем {$fallbackMode}...
game-ticker-start-round-cannot-start-game-mode-restart = Не удалось запустить режим {$failedGameMode}! Перезапуск смены...
game-ticker-start-round-invalid-map = Выбранный цех {$map} не подходит для режима {$mode}. Режим может работать некорректно...
game-ticker-unknown-role = Неизвестный
game-ticker-delay-start = Начало смены отложено на {$seconds} секунд.
game-ticker-pause-start = Начало смены приостановлено.
game-ticker-pause-start-resumed = Отсчет начала смены возобновлен.
game-ticker-player-join-game-message = Добро пожаловать на завод "Прогресс"! Если вы впервые, нажмите ESC и изучите технику безопасности. За помощью обращайтесь к мастеру смены.
game-ticker-get-info-text = Приветствуем на [color=white]заводе "Прогресс"![/color]
                            Текущая смена: [color=white]#{$roundId}[/color]
                            Рабочих у станков: [color=white]{$playerCount}[/color]
                            Цех: [color=white]{$mapName}[/color]
                            Производственный план: [color=white]{$gmTitle}[/color]
                            >[color=yellow]{$desc}[/color]
game-ticker-get-info-preround-text = Приветствуем на [color=white]заводе "Прогресс"![/color]
                            Текущая смена: [color=white]#{$roundId}[/color]
                            Рабочих у станков: [color=white]{$playerCount}[/color] ([color=white]{$readyCount}[/color] {$readyCount ->
                                [one] готов
                               *[other] готовы
                            })
                            Цех: [color=white]{$mapName}[/color]
                            Производственный план: [color=white]{$gmTitle}[/color]
                            >[color=yellow]{$desc}[/color]
game-ticker-no-map-selected = [color=yellow]Цех ещё не выбран![/color]
game-ticker-player-no-jobs-available-when-joining = При попытке присоединиться не найдено вакантных рабочих мест.

player-join-message = Рабочий {$name} заступил на смену.
player-first-join-message = Рабочий {$name} впервые на заводе!

player-leave-message = Рабочий {$name} завершил смену.

latejoin-arrival-announcement = {$character}, {$job}, прибыл на завод!
latejoin-arrival-announcement-special = {$job} {$character} в цеху!
latejoin-arrival-sender = Громкая связь
latejoin-arrivals-direction = Скоро прибывает рабочая смена.
latejoin-arrivals-direction-time = Рабочая смена прибывает через {$time}.
latejoin-arrivals-dumped-from-shuttle = Неведомая сила не позволяет вам покинуть завод.
latejoin-arrivals-teleport-to-spawn = Неведомая сила телепортирует вас в цех. Удачной смены!

preset-not-enough-ready-players = Невозможно выполнить план {$presetName}. Требуется {$minimumPlayers} рабочих, сейчас: {$readyPlayersCount}.
preset-no-one-ready = Невозможно выполнить план {$presetName}. Нет рабочих на участке.

game-run-level-PreRoundLobby = Подготовка к смене
game-run-level-InRound = В процессе смены
game-run-level-PostRound = После смены

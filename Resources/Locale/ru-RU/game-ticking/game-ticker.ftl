game-ticker-restart-round = Перезапуск...
game-ticker-start-round = Начинаем...
game-ticker-start-round-cannot-start-game-mode-fallback = Не удалось запустить {$failedGameMode}! Запускаем {$fallbackMode}...
game-ticker-start-round-cannot-start-game-mode-restart = Не удалось запустить {$failedGameMode}! Перезапуск...
game-ticker-start-round-invalid-map = Выбранная карта {$map} не подходит для режима {$mode}. Режим может работать некорректно...
game-ticker-unknown-role = Неизвестный
game-ticker-delay-start = Начало отложено на {$seconds} секунд.
game-ticker-pause-start = Начало приостановлено.
game-ticker-pause-start-resumed = Отсчет возобновлен.
game-ticker-player-join-game-message = Здравствуйте! Если вы впервые, нажмите ESC и ознакомьтесь с инструкцией. По вопросам обращайтесь к комиссару.
game-ticker-get-info-text = Здравствуйте!
                            Текущая сессия: [color=white]#{$roundId}[/color]
                            Граждан на участке: [color=white]{$playerCount}[/color]
                            Карта: [color=white]{$mapName}[/color]
                            Режим: [color=white]{$gmTitle}[/color]
                            >[color=yellow]{$desc}[/color]
game-ticker-get-info-preround-text = Здравствуйте!
                            Текущая сессия: [color=white]#{$roundId}[/color]
                            Граждан на участке: [color=white]{$playerCount}[/color] ([color=white]{$readyCount}[/color] {$readyCount ->
                                [one] готов
                               *[other] готовы
                            })
                            Карта: [color=white]{$mapName}[/color]
                            Режим: [color=white]{$gmTitle}[/color]
                            >[color=yellow]{$desc}[/color]
game-ticker-no-map-selected = [color=yellow]Карта не выбрана![/color]
game-ticker-player-no-jobs-available-when-joining = При попытке присоединиться не найдено доступных должностей.

player-join-message = Гражданин {$name} прибыл.
player-first-join-message = Гражданин {$name} впервые с нами!

player-leave-message = Гражданин {$name} покинул участок.

latejoin-arrival-announcement = {$character}, {$job}, прибыл на участок!
latejoin-arrival-announcement-special = {$job} {$character} на месте!
latejoin-arrival-sender = Объявление
latejoin-arrivals-direction = Оставьте карту на контроле.
latejoin-arrivals-direction-time = Подготовьте документы через {$time}.
latejoin-arrivals-dumped-from-shuttle = Вам отказано в доступе.
latejoin-arrivals-teleport-to-spawn = Пропуск оформлен. Удачи!

preset-not-enough-ready-players = Невозможно запустить {$presetName}. Требуется {$minimumPlayers} граждан, сейчас: {$readyPlayersCount}.
preset-no-one-ready = Невозможно запустить {$presetName}. Нет готовых граждан.

game-run-level-PreRoundLobby = Подготовка
game-run-level-InRound = Активна
game-run-level-PostRound = Завершена

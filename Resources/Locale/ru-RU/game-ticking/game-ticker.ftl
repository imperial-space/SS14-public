game-ticker-restart-round = Отмотка времени вспять...
game-ticker-start-round = Временная петля начинается вновь...
game-ticker-start-round-cannot-start-game-mode-fallback = Не удалось запустить режим {$failedGameMode}! Запускаем {$fallbackMode}...
game-ticker-start-round-cannot-start-game-mode-restart = Не удалось запустить режим {$failedGameMode}! Перезапуск раунда...
game-ticker-start-round-invalid-map = Выбранная карта {$map} не подходит для режима игры {$mode}. Игровой режим может работать не так, как задумано...
game-ticker-unknown-role = Неизвестный
game-ticker-delay-start = Начало нового витка временной петли было отложено на {$seconds} секунд.
game-ticker-pause-start = Начало нового витка временной петли было приостановлено.
game-ticker-pause-start-resumed = Отсчет начала нового витка временной петли возобновлен.
game-ticker-player-join-game-message = Добро пожаловать в фентези мир! Если вы играете впервые, обязательно нажмите ESC на клавиатуре и прочитайте правила игры, а также не бойтесь просить помощи в LOOC чате или "Админ помощь".
game-ticker-get-info-text = Привет и добро пожаловать на сервер [color=white]Spellward![/color]
                            Текущий временной виток: [color=white]#{ $roundId }[/color]
                            Текущее количество игроков: [color=white]{ $playerCount }[/color]
                            Текущая карта: [color=white]{$mapName}[/color]
                            Текущее испытание: [color=white]{$gmTitle}[/color]
                            >[color=yellow]{ $desc }[/color]
game-ticker-get-info-preround-text = Привет и добро пожаловать на сервер [color=white]Spellward![/color]
                            Текущий временной виток: [color=white]#{$roundId}[/color]
                            Текущее количество игроков: [color=white]{$playerCount}[/color] ([color=white]{$readyCount}[/color] {$readyCount ->
                                [one] готов
                               *[other] готовы
                            })
                            Текущая карта: [color=white]{$mapName}[/color]
                            Текущее испытание: [color=white]{$gmTitle}[/color]
                            >[color=yellow]{$desc}[/color]
game-ticker-no-map-selected = [color=yellow]Карта ещё не выбрана![/color]
game-ticker-player-no-jobs-available-when-joining = При попытке присоединиться к игре ни одной роли не было доступно.

# Displayed in chat to admins when a player joins
player-join-message = Игрок {$name} зашёл.
player-first-join-message = Игрок {$name} зашёл впервые!

# Displayed in chat to admins when a player leaves
player-leave-message = Игрок {$name} вышел.

latejoin-arrival-announcement = {$character}, {$job}, прибыл на станцию!
latejoin-arrival-announcement-special = {$job} {$character} на борту!
latejoin-arrival-sender = Общее
latejoin-arrivals-direction = Вскоре прибудет шаттл, который доставит вас на станцию.
latejoin-arrivals-direction-time = Шаттл, который доставит вас на станцию, прибудет через {$time}.
latejoin-arrivals-dumped-from-shuttle = Таинственная сила не позволяет вам улететь на шаттле прибытия.
latejoin-arrivals-teleport-to-spawn = Таинственная сила телепортирует вас с шаттла прибытия. Удачной смены!

preset-not-enough-ready-players = Невозможно запустить испытание бога {$presetName}. Требуется {$minimumPlayers} готовых игроков, сейчас: {$readyPlayersCount}.
preset-no-one-ready = Невозможно запустить испытание бога {$presetName}. Нет готовых игроков.

game-run-level-PreRoundLobby = Лобби до начала раунда
game-run-level-InRound = В раунде
game-run-level-PostRound = После раунда

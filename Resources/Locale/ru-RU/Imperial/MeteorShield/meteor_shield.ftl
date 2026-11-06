## Станционные щиты (SS13)

station-goal-meteor-shield =
    Система противометеорной защиты
    Станция находится в зоне, заполненной космическим мусором.
    У вас есть прототип защитной системы, который необходимо развернуть для снижения количества аварий из-за столкновений.
    Вы можете заказать спутники и системы через отдел снабжения.

meteor-shield-round-end = Покрытие метеоритного щита: [color=red][bold]{ $coverage }[/bold][/color] из { $goal } клеток.
meteor-shield-round-end-success = Покрытие метеоритного щита: [color=green][bold]{ $coverage }[/bold][/color] из { $goal } клеток.

meteor-shield-examine-active = Устройство активно. Вы можете отключить его, взаимодействуя с ним.
meteor-shield-examine-active-emagged = [color=yellow]Вместо обычных звуковых сигналов оно издаёт странное постоянное шипение белого шума…[/color]
meteor-shield-examine-beeping = Оно периодически подаёт звуковые сигналы, поддерживая связь со спутниковой сетью.
meteor-shield-examine-inactive = Устройство отключено. Вы можете активировать его, взаимодействуя с ним.
meteor-shield-examine-inactive-emagged = [color=yellow]Но что-то в нём кажется подозрительным...[/color]

meteor-shield-looking-on = ищем кнопку включения
meteor-shield-looking-off = ищем кнопку выключения
meteor-shield-only-space = { CAPITALIZE($satellite) } можно активировать только в космосе.
meteor-shield-activate = Вы активируете { $satellite }.
meteor-shield-deactivate = Вы деактивируете { $satellite }.
meteor-shield-multitool = // NTSAT-{ $id } // Mode : { $mode } //{ $debug }
meteor-shield-not-responding = Спутник, кажется, не отвечает...?

meteor-shield-already-emagged = уже емагнуто!
meteor-shield-emag-cooldown = на перезарядке! Последнему емагнутому спутнику требуется { $seconds } с для перекалибровки. Емаг другого спутника в слишком короткое время может повредить сеть.
meteor-shield-emagged = Вы получаете доступ к режиму отладки спутника, и он начинает излучать странный сигнал, увеличивая вероятность метеоритных ударов.
meteor-shield-chance-doubled = вероятность метеоров удвоена
meteor-shield-chance-halved = вероятность метеоров уменьшилась вдвое

meteor-shield-say-recalibrating = Перекалибровка... Примерное время: { $seconds } секунд.
meteor-shield-say-threshold-one = Предупреждение. Вероятность метеоритного удара входит в опасный диапазон для более экзотических метеоритов.
meteor-shield-say-threshold-two = Предупреждение. Риск сгущения тёмной материи входит в существующие диапазоны. Дальнейшее вмешательство будет доложено.
meteor-shield-say-threshold-three = Предупреждение. Дальнейшее вмешательство было доложено.
meteor-shield-say-threshold-four = Предупреждение. Предупреждение. Тёмно-материальный метеор на курсе к станции.
meteor-shield-announce-tampering = Внимание! Вмешательство в работу метеоритных щитов подвергает станцию риску необычных и смертоносных столкновений с метеоритами. Пожалуйста, проверьте маяки на наличие странных сигналов и демонтируйте взломанные метеоритные щиты.
meteor-shield-announce-tampering-sender = Предупреждение о странном метеоритном сигнале

meteor-shield-beam-reflected = Луч { $satellite } отражён тёмной материей!
meteor-shield-dark-matteor-sender = Метеоры
meteor-shield-dark-matteor-announce = Тревога. Помехи в работе метеоритных спутников привлекли тёмный маттеор. Объект стремительно приближается к станции. Приготовьтесь к столкновению.
meteor-shield-dark-matteor-missed = Ого. Тёмный Маттеор действительно промахнулся мимо вашей станции. Не забудьте поблагодарить своего священника за его явное божественное вмешательство.

satellite-control-title = Управление спутниками
satellite-control-coverage = Покрытие
satellite-control-section = Управление спутниками

ent-ImperialMeteorShieldSatellite = спутник метеоритного щита
    .desc = Спутник метеоритной защиты.
ent-ImperialMeteorShieldSignal = сигнал повреждённого метеоритного щита
    .desc = { "" }
ent-ImperialSatelliteControl = управление спутниками
    .desc = Используется для управления спутниковой сетью.
ent-ImperialSatelliteControlCircuitboard = управление спутниками (компьютерная плата)
    .desc = Печатная плата консоли управления системой противометеорной защиты.
ent-ImperialDarkMatteor = тёмный маттеор
    .desc = Наиболее распространённая теория гласит, что тёмная материя состоит из слабо взаимодействующих массивных частиц (WIMP). Но, глядя на эту зловещую силу неминуемой смерти, несущуюся на вас, приходится признать — выглядит она совсем не слабо...
ent-ImperialCrateMeteorShieldSatellites = ящик спутников метеоритного щита
    .desc = Защитите существование этой станции с помощью системы противометеорной защиты. Содержит три спутника метеоритного щита.
ent-ImperialCrateSatelliteControl = ящик с платой управления системы противометеорной защиты
    .desc = Система управления системой спутников противометеорной защиты.

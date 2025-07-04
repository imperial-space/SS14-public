analysis-console-menu-title = Аналитическая консоль
analysis-console-server-list-button = Список серверов
analysis-console-extract-button = Очки

analysis-console-info-no-scanner = Анализатор не подключен. Соедините анализатор и консоль используя мультитул.
analysis-console-info-no-artifact = Артефакт не найден. Поместите артефакт на платформу для начала работы.
analysis-console-info-ready = Все системы в норме. Доступно сканирование.

analysis-console-no-node = Выберите узел для просмотра.
analysis-console-info-id = [font="Monospace" size=11]ID_УЗЛА:[/font]
analysis-console-info-id-value = [font="Monospace" size=11][color=yellow]{$id}[/color][/font]
analysis-console-info-class = [font="Monospace" size=11]КЛАСС_УЗЛА:[/font]
analysis-console-info-class-value = [font="Monospace" size=11]{$class}[/font]
analysis-console-info-locked = [font="Monospace" size=11]СТАТУС_УЗЛА:[/font]
analysis-console-info-locked-value = [font="Monospace" size=11][color={ $state ->
    [0] red]Закрыт
    [1] lime]Открыт
    *[2] plum]Активен
}[/color][/font]
analysis-console-info-durability = [font="Monospace" size=11]ИСПОЛЬЗОВАНИЯ:[/font]
analysis-console-info-durability-value = [font="Monospace" size=11][color={$color}]{$current}/{$max}[/color][/font]
analysis-console-info-effect = [font="Monospace" size=11]РЕАКЦИЯ:[/font]
analysis-console-info-effect-value = [font="Monospace" size=11][color=gray]{ $state ->
    [true] {$info}
    *[false] Разблокируйте узел для получения информации.
}[/color][/font]
analysis-console-info-trigger = [font="Monospace" size=11]СТИМУЛЯТОРЫ:[/font]
analysis-console-info-triggered-value = [font="Monospace" size=11][color=gray]{$triggers}[/color][/font]
analysis-console-info-scanner = Сканирование...
analysis-console-info-scanner-paused = Остановлено.
analysis-console-progress-text = {$seconds ->
    [one] T-{$seconds} секунда
    *[other] T-{$seconds} секунд
}

analysis-console-extract-value = [font="Monospace" size=11][color=orange]Узел {$id} (+{$value})[/color][/font]
analysis-console-extract-none = [font="Monospace" size=11][color=orange] Ни у одного открытого узла не осталось очков для извлечения. [/color][/font]
analysis-console-extract-sum = [font="Monospace" size=11][color=orange]Итоговые очки: {$value}[/color][/font]

analyzer-artifact-extract-popup = На поверхности артефакта мерцает неизвестная энергия!

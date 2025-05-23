delivery-recipient-examine = Это предназначено для {$recipient}, {$job}.
delivery-already-opened-examine = Это уже открыто.
delivery-earnings-examine = Доставя это на счет станции поступит [color=yellow]{$spesos}[/color] кредитов.
delivery-recipient-no-name = Безымянный
delivery-recipient-no-job = Неизвестный

delivery-unlocked-self = Вы разблокировали {$delivery} своим отпечатком.
delivery-opened-self = Вы открыли {$delivery}.
delivery-unlocked-others = {CAPITALIZE($recipient)} разблокировал {$delivery} с {POSS-ADJ($possadj)} отпечатком.
delivery-opened-others = {CAPITALIZE($recipient)} открыл {$delivery}.

delivery-unlock-verb = Разблокировано
delivery-open-verb = Открыто
delivery-slice-verb = Частично открыто

delivery-teleporter-amount-examine =
    { $amount ->
        [one] Содержит [color=yellow]{$amount}[/color] посылку.
        *[other] Содержит [color=yellow]{$amount}[/color] посылок.
    }
delivery-teleporter-empty = {$entity} пуст.
delivery-teleporter-empty-verb = Взять письмо


# modifiers
delivery-priority-examine = [color=orange]Приоритетное письмо {$type}[/color]. У вас осталось [color=orange]{$time}[/color] на доставку для получения бонуса.
delivery-priority-delivered-examine = [color=orange]Приоритетное письмо {$type}[/color]. Он был доставлен вовремя.
delivery-priority-expired-examine = [color=orange]Приоритетное письмо {$type}[/color]. Время истекло.

delivery-fragile-examine = [color=red]Хрупкая посылка {$type}[/color]. Доставьте его целым для получения бонуса.
delivery-fragile-broken-examine = [color=red]Хрупкая посылка {$type}[/color]. Выглядит сильно повреждённым.
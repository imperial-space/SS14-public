centcomannouncecommand-desc = Отправляет шаблонное оповещение от Центрального Командования с автоподстановкой номера станции
centcomannouncecommand-help = centcomannounce <id шаблона> — отправляет заранее заданное оповещение ЦК, заменяя XX-### на актуальный номер станции
centcomannouncecommand-id-preset = id шаблона оповещения
centcomannouncecommand-error-args = Использование: centcomannounce <id шаблона>
centcomannouncecommand-error-preset-not-found = Шаблон оповещения "{$protoid}" не найден
centcomannouncecommand-error-no-station = Не удалось найти ни одной станции

centcomsendpapercommand-desc = Отправляет шаблонную бумагу от Центрального Командования на факс с автоподстановкой полей
centcomsendpapercommand-help = centcomsendpaper <id бумаги> <название факса> [имя оператора|-] [текст решения] — отправляет заранее заданную бумагу ЦК на факс, заменяя номер станции, дату, имя оператора и ответственного за выполнение
centcomsendpapercommand-id-preset = id шаблона бумаги
centcomsendpapercommand-fax-hint = название факса
centcomsendpapercommand-operator-hint = имя оператора (или "-" чтобы пропустить и получить случайное имя)
centcomsendpapercommand-decision-hint = текст решения (если требуется шаблоном)
centcomsendpapercommand-error-args = Использование: centcomsendpaper <id бумаги> <название факса> [имя оператора|-] [текст решения]
centcomsendpapercommand-error-paper-not-found = Шаблон бумаги "{$protoid}" не найден
centcomsendpapercommand-error-fax-not-found = Факс с названием "{$query}" не найден
centcomsendpapercommand-error-multiple-faxes = Найдено несколько факсов с названием "{$query}", уточните запрос
centcomsendpapercommand-acting-captain = ВрИО капитан станции

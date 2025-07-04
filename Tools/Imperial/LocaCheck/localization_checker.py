import os
import sys
import json
import argparse
from collections import OrderedDict
import textwrap
import time

def get_script_directory():
    """Возвращает путь к папке, где находится скрипт"""
    return os.path.dirname(os.path.abspath(sys.argv[0]))

def load_checklist(file_path):
    """Загружает прогресс из файла"""
    if not os.path.exists(file_path):
        return {}
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            return json.load(f)
    except Exception as e:
        print(f"[!] Ошибка загрузки файла прогресса: {e}")
        return {}

def save_checklist(file_path, checklist):
    """Сохраняет прогресс в файл"""
    try:
        with open(file_path, 'w', encoding='utf-8') as f:
            json.dump(checklist, f, ensure_ascii=False, indent=2)
        return True
    except Exception as e:
        print(f"[!] Ошибка сохранения файла прогресса: {e}")
        return False

def should_skip_line(line):
    """Определяет, нужно ли пропускать строку (комментарии)"""
    return not line.strip() or line.startswith('#')

def parse_keys(file_path, delimiter):
    """Парсинг файла локализации"""
    keys = OrderedDict()
    key_set = set()

    if not os.path.exists(file_path):
        print(f"[X] Файл не найден: {file_path}")
        return keys, key_set

    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            for line in f:
                line = line.strip()

                if should_skip_line(line):
                    continue

                if delimiter not in line:
                    continue

                parts = line.split(delimiter, 1)
                if len(parts) < 2:
                    continue

                path, key = parts
                key_id = f"{path}{delimiter}{key}"

                keys[key_id] = (path, key)
                key_set.add(key_id)

        print(f"[V] Загружено ключей: {len(keys)}")
        return keys, key_set
    except Exception as e:
        print(f"[X] Ошибка чтения файла {file_path}: {e}")
        return keys, key_set

def get_untranslated_keys(original_keys, target_key_set, checklist, filter_mode):
    """Возвращает только непереведенные ключи"""
    untranslated = OrderedDict()
    translated_count = 0
    untranslated_count = 0
    idx = 1

    for key_id, (path, key) in original_keys.items():
        # Применяем фильтр по режиму
        if filter_mode == 1:  # Только datasets
            if 'datasets' not in path:
                continue
        elif filter_mode == 2:  # Скрыть datasets
            if 'datasets' in path:
                continue

        # Проверяем наличие ключа в целевой локализации
        if key_id in target_key_set:
            translated_count += 1
            continue

        # Учитываем отметки в чеклисте
        if checklist.get(key_id) == "V":
            translated_count += 1
        else:
            untranslated_count += 1
            untranslated[idx] = {
                'path': path,
                'key': key,
                'id': key_id,
                'status': checklist.get(key_id, "X")
            }
            idx += 1

    return untranslated, translated_count, untranslated_count

def print_progress(current, total):
    """Печатает прогресс-бар"""
    if total == 0:
        print("\n[V] Все ключи переведены!")
        return

    bar_length = 30
    progress = current / total if total > 0 else 0
    filled = int(bar_length * progress)
    bar = '█' * filled + '-' * (bar_length - filled)
    percent = progress * 100

    print(f"\nПрогресс: [{bar}] {percent:.1f}% ({current}/{total})")
    print(f"Осталось перевести: {total - current}\n")

def print_file_help(script_dir):
    """Показывает инструкцию по размещению файлов"""
    print("\n" + "═"*50)
    print("[F] ИНСТРУКЦИЯ ПО РАЗМЕЩЕНИЮ ФАЙЛОВ")
    print("═"*50)
    print(f"1. Поместите файлы локализации в папку скрипта:")
    print(f"   [F] {script_dir}")
    print("2. Убедитесь, что файлы называются:")
    print("   - original.txt - исходная локализация")
    print("   - target.txt - целевая локализация")
    print("3. Или укажите пути к файлам при запуске:")
    print("   python localization_checker.py --original путь/к/original.txt --target путь/к/target.txt")
    print("═"*50 + "\n")

def format_key_display(path, key, status, idx, max_width=100):
    """Форматирует отображение ключа с переносами и цветами"""
    # Цветовые коды
    color_green = "\033[92m"
    color_red = "\033[91m"
    color_gray = "\033[90m"
    color_reset = "\033[0m"
    color_cyan = "\033[96m"
    color_yellow = "\033[93m"

    # Выбираем цвет в зависимости от статуса
    color = color_green if status == "V" else color_red
    status_display = "[V]" if status == "V" else "[X]"

    # Форматируем номер
    num_str = f"{idx:4d}."

    # Форматируем путь
    path_display = f"{color_gray}Путь: {color_reset}{color_cyan}{path}{color_reset}"

    # Форматируем ключ с переносами
    key_lines = textwrap.wrap(
        f"{color_gray}Ключ: {color_reset}{color}{key}{color_reset}",
        width=max_width,
        subsequent_indent='      '
    )
    key_display = '\n'.join(key_lines)

    # Собираем все вместе
    return f"{color}{num_str} {status_display}{color_reset}\n{path_display}\n{key_display}"

def main():
    script_dir = get_script_directory()
    parser = argparse.ArgumentParser(description='Проверка локализации')
    parser.add_argument('--original', default=os.path.join(script_dir, 'original.txt'),
                        help='Файл исходной локализации')
    parser.add_argument('--target', default=os.path.join(script_dir, 'target.txt'),
                        help='Файл целевой локализации')
    parser.add_argument('--progress', default=os.path.join(script_dir, 'translation_progress.json'),
                        help='Файл прогресса')
    parser.add_argument('--delimiter', default='鎰', help='Разделитель ключей')
    parser.add_argument('--autosave', type=int, default=5,
                        help='Интервал автосохранения (в минутах)')
    args = parser.parse_args()

    # Режимы фильтрации
    filter_mode = 0
    filter_modes = {
        0: "БЕЗ ФИЛЬТРА",
        1: "ТОЛЬКО DATASETS",
        2: "СКРЫТЬ DATASETS"
    }

    # Определяем ширину терминала
    try:
        terminal_width = os.get_terminal_size().columns
    except:
        terminal_width = 100

    print("\n" + "═" * terminal_width)
    title = f" ПРОВЕРКА ЛОКАЛИЗАЦИИ | Разделитель: '{args.delimiter}' "
    print(title.center(terminal_width, ' '))
    print("═" * terminal_width)

    if not all(map(os.path.exists, [args.original, args.target])):
        missing = [f for f in [args.original, args.target] if not os.path.exists(f)]
        for f in missing:
            print(f"\n[X] ФАЙЛ НЕ НАЙДЕН: {f}")
        print_file_help(script_dir)
        input("Нажмите Enter для выхода...")
        return

    print("\n[~] Загрузка данных прогресса...")
    checklist = load_checklist(args.progress)

    print("[*] Загрузка файлов локализации...")

    # Загружаем ключи
    original_keys, _ = parse_keys(args.original, args.delimiter)
    _, target_key_set = parse_keys(args.target, args.delimiter)

    if not original_keys:
        print("\n[X] В исходном файле локализации не найдено ключей.")
        print(f"    Убедитесь, что файл содержит разделитель: '{args.delimiter}'")
        print_file_help(script_dir)
        input("Нажмите Enter для выхода...")
        return

    # Инициализация списка ключей
    untranslated, translated_count, untranslated_count = get_untranslated_keys(
        original_keys,
        target_key_set,
        checklist,
        filter_mode
    )
    total_keys = untranslated_count
    last_save_time = time.time()
    autosave_interval = args.autosave * 60  # в секундах

    while True:
        os.system('cls' if os.name == 'nt' else 'clear')

        # Автосохранение
        current_time = time.time()
        if current_time - last_save_time >= autosave_interval:
            if save_checklist(args.progress, checklist):
                last_save_time = current_time
                print("\033[92m\n[A] АВТОСОХРАНЕНИЕ ПРОГРЕССА!\033[0m")
                time.sleep(1)  # Краткая пауза для отображения сообщения

        # Отображаем статус фильтра в заголовке
        print("═" * terminal_width)
        title = f" ПРОВЕРКА ЛОКАЛИЗАЦИИ | Файлы: {os.path.basename(args.original)}, {os.path.basename(args.target)} "
        filter_info = f" [Фильтр: {filter_modes[filter_mode]}] "
        print(title.center(terminal_width, ' '))
        print(filter_info.center(terminal_width, ' '))
        print("═" * terminal_width)

        if total_keys == 0:
            print("\n[V] ВСЕ КЛЮЧИ ПЕРЕВЕДЕНЫ! ЛОКАЛИЗАЦИЯ ЗАВЕРШЕНА.")
            save_checklist(args.progress, checklist)
            input("\nНажмите Enter для выхода...")
            return

        print(f"\n[L] НЕПЕРЕВЕДЕННЫЕ КЛЮЧИ (Всего: {len(untranslated)}):")
        print("(Ключи отсортированы в порядке их появления в файле)")

        # Определяем, сколько ключей показывать
        keys_to_show = min(30, len(untranslated))
        print(f"\nПервые ключи (показано {keys_to_show} из {len(untranslated)}):")

        # Показываем ключи с 1 по keys_to_show
        for i, idx in enumerate(list(untranslated.keys())[:keys_to_show], 1):
            data = untranslated[idx]
            status = data['status']

            # Форматируем вывод ключа
            key_display = format_key_display(
                data['path'],
                data['key'],
                status,
                i,
                max_width=terminal_width - 10
            )

            print(key_display)
            print("-" * terminal_width)

        # Статистика и прогресс
        total = translated_count + untranslated_count
        print_progress(translated_count, total)

        # Меню действий
        print("\033[93m[A] ДЕЙСТВИЯ:\033[0m")
        print(f"1-{keys_to_show}. Отметить/снять отметку по номеру ключа")
        print("F. Сменить режим фильтра")
        print("S. Сохранить прогресс")
        print("R. Обновить список ключей")
        print("I. Показать информацию о файлах")
        print("C. Изменить разделитель")
        print("Q. Выход")

        # Подсказка по фильтрам
        print("\n\033[93m[F] РЕЖИМЫ ФИЛЬТРАЦИИ:\033[0m")
        print("0: Без фильтра (показать все ключи)")
        print("1: Только ключи с 'datasets' в пути")
        print("2: Скрыть ключи с 'datasets' в пути")

        choice = input("\n>>> ВЫБЕРИТЕ ДЕЙСТВИЕ: ").upper()

        # Обработка выбора ключа (1-30)
        if choice.isdigit():
            idx = int(choice)
            if 1 <= idx <= keys_to_show:
                # Получаем реальный индекс ключа
                actual_idx = list(untranslated.keys())[idx-1]
                data = untranslated[actual_idx]
                current_status = data['status']
                new_status = "V" if current_status == "X" else "X"

                # Обновляем статус в чеклисте и в данных ключа
                checklist[data['id']] = new_status
                data['status'] = new_status
                untranslated[actual_idx] = data

                # Обновляем счетчики
                if new_status == "V":
                    translated_count += 1
                    untranslated_count -= 1
                else:
                    translated_count -= 1
                    untranslated_count += 1

                total_keys = untranslated_count

                action = "ОТМЕЧЕН КАК ПЕРЕВЕДЁННЫЙ" if new_status == "V" else "СНЯТА ОТМЕТКА ПЕРЕВОДА"
                color = "\033[92m" if new_status == "V" else "\033[91m"
                print(f"\n{color}[!] КЛЮЧ #{idx} {action}!\033[0m")
            else:
                print(f"\033[91m[X] КЛЮЧ С НОМЕРОМ {idx} НЕ НАЙДЕН!\033[0m")
            input("\nНажмите Enter для продолжения...")

        # Сохранить прогресс
        elif choice == 'S':
            if save_checklist(args.progress, checklist):
                print("\033[92m\n[S] ПРОГРЕСС СОХРАНЁН!\033[0m")
                last_save_time = time.time()
            else:
                print("\033[91m\n[!] НЕ УДАЛОСЬ СОХРАНИТЬ ПРОГРЕСС!\033[0m")
            input("Нажмите Enter для продолжения...")

        # Обновить список ключей
        elif choice == 'R':
            print("\n[R] ОБНОВЛЕНИЕ СПИСКА КЛЮЧЕЙ...")
            # Перезагружаем файлы
            original_keys, _ = parse_keys(args.original, args.delimiter)
            _, target_key_set = parse_keys(args.target, args.delimiter)

            # Обновляем список непереведенных ключей
            untranslated, translated_count, untranslated_count = get_untranslated_keys(
                original_keys,
                target_key_set,
                checklist,
                filter_mode
            )
            total_keys = untranslated_count
            print(f"[V] ЗАГРУЖЕНО {len(untranslated)} КЛЮЧЕЙ")
            input("Нажмите Enter для продолжения...")

        # Сменить режим фильтра
        elif choice == 'F':
            print("\n[F] СМЕНА РЕЖИМА ФИЛЬТРАЦИИ:")
            print("0: Без фильтра (показать все ключи)")
            print("1: Только ключи с 'datasets' в пути")
            print("2: Скрыть ключи с 'datasets' в пути")

            try:
                new_mode = int(input(">>> ВВЕДИТЕ НОМЕР РЕЖИМА (0-2): "))
                if new_mode in [0, 1, 2]:
                    filter_mode = new_mode
                    print(f"\033[92m\n[!] ФИЛЬТР ИЗМЕНЕН НА: {filter_modes[filter_mode]}\033[0m")

                    # Пересчитываем список с новым фильтром
                    untranslated, translated_count, untranslated_count = get_untranslated_keys(
                        original_keys,
                        target_key_set,
                        checklist,
                        filter_mode
                    )
                    total_keys = untranslated_count
                else:
                    print("\033[91m[X] НЕВЕРНЫЙ РЕЖИМ ФИЛЬТРА!\033[0m")
            except ValueError:
                print("\033[91m[X] ВВЕДИТЕ ЧИСЛО ОТ 0 ДО 2!\033[0m")

            input("\nНажмите Enter для продолжения...")

        # Изменить разделитель
        elif choice == 'C':
            new_delimiter = input("\n>>> ВВЕДИТЕ НОВЫЙ РАЗДЕЛИТЕЛЬ: ")
            if new_delimiter:
                args.delimiter = new_delimiter
                print(f"\033[92m\n[!] РАЗДЕЛИТЕЛЬ ИЗМЕНЕН НА: '{args.delimiter}'\033[0m")

                # Перезагружаем файлы с новым разделителем
                original_keys, _ = parse_keys(args.original, args.delimiter)
                _, target_key_set = parse_keys(args.target, args.delimiter)

                # Обновляем список ключей
                untranslated, translated_count, untranslated_count = get_untranslated_keys(
                    original_keys,
                    target_key_set,
                    checklist,
                    filter_mode
                )
                total_keys = untranslated_count
                print(f"[V] ЗАГРУЖЕНО {len(untranslated)} КЛЮЧЕЙ С НОВЫМ РАЗДЕЛИТЕЛЕМ")
            else:
                print("\033[91m[X] РАЗДЕЛИТЕЛЬ НЕ МОЖЕТ БЫТЬ ПУСТЫМ!\033[0m")
            input("\nНажмите Enter для продолжения...")

        # Информация о файлах
        elif choice == 'I':
            print("\n" + "═" * terminal_width)
            print("[F] ИНФОРМАЦИЯ О ФАЙЛАХ")
            print("═" * terminal_width)
            print(f"[C] Текущий рабочий каталог: {os.getcwd()}")
            print(f"[F] Папка скрипта: {script_dir}")
            print(f"[*] Исходная локализация: {args.original}")
            print(f"[*] Целевая локализация: {args.target}")
            print(f"[P] Файл прогресса: {args.progress}")
            print(f"[D] Используемый разделитель: '{args.delimiter}'")
            print(f"[A] Автосохранение: каждые {args.autosave} мин")
            print("═" * terminal_width)
            input("\nНажмите Enter для продолжения...")

        # Выход
        elif choice == 'Q':
            print("\nВыход из программы")
            if save_checklist(args.progress, checklist):
                print("\033[92m[S] ПРОГРЕСС СОХРАНЁН ПЕРЕД ВЫХОДОМ\033[0m")
            break

        else:
            print("\033[91m[X] НЕВЕРНЫЙ ВЫБОР. ПОПРОБУЙТЕ СНОВА.\033[0m")
            input("Нажмите Enter для продолжения...")

if __name__ == "__main__":
    main()

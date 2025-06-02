import re
import os
import sys
import json
import argparse
from collections import OrderedDict

def get_script_directory():
    """Возвращает путь к папке, где находится скрипт"""
    return os.path.dirname(os.path.abspath(sys.argv[0]))

def clean_path(path):
    """Удаляет 'Robast' из пути"""
    return path.replace('Robast', '')

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
    """Определяет, нужно ли пропускать строку (комментарии или Robast)"""
    # Пропускаем пустые строки и комментарии
    if not line or line.startswith('#'):
        return True
    # Пропускаем строки, содержащие Robast
    return 'Robast' in line

def parse_keys(file_path):
    """Быстрый парсинг файла локализации с фильтрацией"""
    keys = OrderedDict()

    if not os.path.exists(file_path):
        print(f"[X] Файл не найден: {file_path}")
        return keys

    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            # Чтение файла в обратном порядке без создания полного списка строк
            for line in reversed(f.readlines()):
                line = line.strip()

                if should_skip_line(line) or '鎰' not in line:
                    continue

                parts = line.split('鎰', 1)
                if len(parts) < 2:
                    continue

                path, key = parts
                clean = clean_path(path)
                keys[f"{path}鎰{key}"] = (path, key, clean)

        print(f"[V] Загружено ключей: {len(keys)}")
        return keys
    except Exception as e:
        print(f"[X] Ошибка чтения файла {file_path}: {e}")
        return keys

def get_untranslated_keys(original_keys, russian_clean_keys, checklist):
    """Возвращает только непереведенные ключи с нумерацией"""
    untranslated = OrderedDict()
    translated_count = 0
    idx = 1

    # Одновременно создаем список и считаем отмеченные ключи
    for key_id, (path, key, clean) in original_keys.items():
        if clean in russian_clean_keys:
            continue

        if checklist.get(key_id) == "V":
            translated_count += 1

        untranslated[idx] = {
            'path': path,
            'key': key,
            'id': key_id
        }
        idx += 1

    return untranslated, translated_count

def print_progress(current, total):
    """Печатает прогресс-бар"""
    if total == 0:
        print("\n[V] Все ключи переведены!")
        return

    bar_length = 30
    progress = current / total
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
    print("   - russian.txt - русская локализация")
    print("3. Или укажите пути к файлам при запуске:")
    print("   python localization_checker.py --original путь/к/original.txt --russian путь/к/russian.txt")
    print("═"*50 + "\n")

def main():
    # Определяем путь к папке скрипта
    script_dir = get_script_directory()

    # Настройка аргументов командной строки
    parser = argparse.ArgumentParser(description='Проверка локализации')
    parser.add_argument('--original', default=os.path.join(script_dir, 'original.txt'),
                        help='Файл исходной локализации')
    parser.add_argument('--russian', default=os.path.join(script_dir, 'russian.txt'),
                        help='Файл русской локализации')
    parser.add_argument('--progress', default=os.path.join(script_dir, 'translation_progress.json'),
                        help='Файл прогресса')
    args = parser.parse_args()

    # Выводим информацию о файлах
    print("\n" + "═"*50)
    print(f"[C] Текущий рабочий каталог: {os.getcwd()}")
    print(f"[F] Папка скрипта: {script_dir}")
    print(f"[*] Исходная локализация: {args.original}")
    print(f"[*] Русская локализация: {args.russian}")
    print(f"[P] Файл прогресса: {args.progress}")
    print("═"*50)

    # Проверка существования файлов
    if not all(map(os.path.exists, [args.original, args.russian])):
        missing = [f for f in [args.original, args.russian] if not os.path.exists(f)]
        for f in missing:
            print(f"\n[X] ФАЙЛ НЕ НАЙДЕН: {f}")
        print_file_help(script_dir)
        input("Нажмите Enter для выхода...")
        return

    # Загрузка прогресса
    print("\n[~] Загрузка данных прогресса...")
    checklist = load_checklist(args.progress)

    # Загрузка ключей
    print("[*] Загрузка файлов локализации...")
    original_keys = parse_keys(args.original)
    russian_keys = parse_keys(args.russian)

    # Проверка наличия данных
    if not original_keys or not russian_keys:
        print("\n[X] Нет данных для сравнения. Проверьте файлы локализации.")
        print_file_help(script_dir)
        input("Нажмите Enter для выхода...")
        return

    # Для русской локализации нужны только "чистые" ключи
    russian_clean_keys = {clean for _, (_, _, clean) in russian_keys.items()}

    # Инициализация списка ключей
    untranslated, translated_count = get_untranslated_keys(
        original_keys,
        russian_clean_keys,
        checklist
    )
    total_keys = len(untranslated)

    # Основной интерактивный цикл
    while True:
        os.system('cls' if os.name == 'nt' else 'clear')

        # Заголовок
        print("═"*50)
        print(f"[*] ПРОВЕРКА ЛОКАЛИЗАЦИИ | Файлы: {os.path.basename(args.original)}, {os.path.basename(args.russian)}")
        print("═"*50)

        if total_keys == 0:
            print("\n[V] ВСЕ КЛЮЧИ ПЕРЕВЕДЕНЫ! ЛОКАЛИЗАЦИЯ ЗАВЕРШЕНА.")
            save_checklist(args.progress, checklist)
            input("\nНажмите Enter для выхода...")
            return

        # Статус
        print(f"\n[L] НЕПЕРЕВЕДЕННЫЕ КЛЮЧИ (Всего: {total_keys}):")
        print("(Новые ключи вверху с номерами 1, 2, 3...)")

        # Вывод ключей (первые 30)
        print("\nПоследние добавленные ключи:")
        for idx in list(untranslated.keys())[:30]:
            data = untranslated[idx]
            status = checklist.get(data['id'], "X")
            status_display = "[V]" if status == "V" else "[X]"

            # Цветовое оформление
            color = "\033[92m" if status == "V" else "\033[91m"
            reset = "\033[0m"
            gray = "\033[90m"

            print(f"{color}{idx:4d}. {status_display} Путь: {gray}{data['path']}{reset}")
            print(f"      Ключ: {color}{data['key']}{reset}")

        # Статистика
        print_progress(translated_count, total_keys)

        # Меню
        print("[A] ДЕЙСТВИЯ:")
        print("1-30. Отметить/снять отметку по номеру ключа")
        print("S. Сохранить прогресс")
        print("R. Обновить список ключей")
        print("I. Показать информацию о файлах")
        print("Q. Выход")

        choice = input("\n>>> ВЫБЕРИТЕ ДЕЙСТВИЕ: ").upper()

        # Обработка выбора ключа (1-30)
        if choice.isdigit():
            idx = int(choice)
            if idx in untranslated:
                data = untranslated[idx]
                current_status = checklist.get(data['id'], "X")
                new_status = "V" if current_status == "X" else "X"
                checklist[data['id']] = new_status

                # Обновляем счетчик переведенных
                if new_status == "V":
                    translated_count += 1
                elif current_status == "V":
                    translated_count -= 1

                action = "ОТМЕЧЕН КАК ПЕРЕВЕДЁННЫЙ" if new_status == "V" else "СНЯТА ОТМЕТКА ПЕРЕВОДА"
                print(f"\n[!] КЛЮЧ #{idx} {action}!")
            else:
                print(f"[X] КЛЮЧ С НОМЕРОМ {idx} НЕ НАЙДЕН!")
            input("\nНажмите Enter для продолжения...")

        # Сохранить прогресс
        elif choice == 'S':
            if save_checklist(args.progress, checklist):
                print("\n[S] ПРОГРЕСС СОХРАНЁН!")
            else:
                print("\n[!] НЕ УДАЛОСЬ СОХРАНИТЬ ПРОГРЕСС!")
            input("Нажмите Enter для продолжения...")

        # Обновить список ключей
        elif choice == 'R':
            print("\n[R] ОБНОВЛЕНИЕ СПИСКА КЛЮЧЕЙ...")
            original_keys = parse_keys(args.original)
            russian_keys = parse_keys(args.russian)
            russian_clean_keys = {clean for _, (_, _, clean) in russian_keys.items()}
            untranslated, translated_count = get_untranslated_keys(
                original_keys,
                russian_clean_keys,
                checklist
            )
            total_keys = len(untranslated)
            print(f"[V] ЗАГРУЖЕНО {total_keys} КЛЮЧЕЙ")
            input("Нажмите Enter для продолжения...")

        # Информация о файлах
        elif choice == 'I':
            print("\n" + "═"*50)
            print("[F] ИНФОРМАЦИЯ О ФАЙЛАХ")
            print("═"*50)
            print(f"[C] Текущий рабочий каталог: {os.getcwd()}")
            print(f"[F] Папка скрипта: {script_dir}")
            print(f"[*] Исходная локализация: {args.original}")
            print(f"[*] Русская локализация: {args.russian}")
            print(f"[P] Файл прогресса: {args.progress}")
            print(f"[N] Проверьте правильность путей и названий файлов")
            print("═"*50)
            input("\nНажмите Enter для продолжения...")

        # Выход
        elif choice == 'Q':
            print("\nВыход из программы")
            break

        else:
            print("[X] НЕВЕРНЫЙ ВЫБОР. ПОПРОБУЙТЕ СНОВА.")
            input("Нажмите Enter для продолжения...")

if __name__ == "__main__":
    main()

# ShellOverlay

Свой shell-оверлей вместо панели задач Windows Explorer. Рисуется на **Avalonia UI**, настраивается файлами, без GUI-редактора.

При старте программа скрывает системный таскбар (и вторичные панели / overflow иконок), резервирует полосу снизу как AppBar и показывает свою панель из XML + CSS + CSX.

## Запуск

Нужен [.NET 10 SDK](https://dotnet.microsoft.com/download) и Windows 10 2004+ / Windows 11.

```powershell
dotnet run -c Debug
```

Выход:

- кнопка **X** на панели
- глобальный хоткей **Ctrl+Alt+Q** (задаётся в `config/app.json`)

Если панель задач пропала после аварийного завершения:

```powershell
Stop-Process -Name explorer -Force
Start-Process explorer
```

Логи: `config/shelloverlay.log` (рядом с рабочим конфигом).

## Конфиг

Все пользовательские настройки лежат в `config/` (копируется в output при сборке).

| Файл | Назначение |
|------|------------|
| `app.json` | Что прятать, хоткей выхода, имена файлов разметки/стилей/модулей |
| `layout.xml` | Раскладка панели |
| `styles.css` | Стили и простые анимации |
| `shortcuts.json` | Закреплённые ярлыки (`<pins/>`) |
| `modules/*.csx` | Логика элементов |

После правок перезапустите приложение.

### app.json

```json
{
  "hideTaskbar": true,
  "hideSecondaryTaskbars": true,
  "hideNotifyOverflow": true,
  "registerAppBar": true,
  "exitHotkey": "Ctrl+Alt+Q",
  "layoutFile": "layout.xml",
  "stylesFile": "styles.css",
  "shortcutsFile": "shortcuts.json",
  "modulesDirectory": "modules"
}
```

Системный `explorer.exe` не убивается: прячутся окна таскбара, рабочий стол остаётся. Так безопаснее восстанавливать оболочку при выходе.

## XML-разметка

`layout.xml` — это не Avalonia XAML, а свой DSL. Парсер собирает из него контролы Avalonia. Неизвестный тег становится виджетом (заготовка под будущие модули).

```xml
<shell>
  <bar id="main" class="bar" edge="bottom" height="64" margin="10">
    <button id="start" class="icon-btn" module="start">S</button>
    <separator/>
    <pins/>
    <filler/>
    <label id="clock" class="clock" module="clock">--:--</label>
    <button id="exit" class="icon-btn" module="exit">X</button>
  </bar>
</shell>
```

| Тег | Смысл |
|-----|--------|
| `bar` | Полоса. `edge`: `bottom` \| `top`, `height` и `margin` в DIP |
| `button` | Кнопка. `module` — CSX, `launch` — путь к программе |
| `pins` | Ярлыки из `shortcuts.json` |
| `label` / `clock` / `text` | Текст; `OnRender()` модуля может его обновлять |
| `separator` | Вертикальный разделитель |
| `filler` | Всё справа от него прижимается к правому краю |
| `panel` | Горизонтальный/вертикальный контейнер |
| `widget` или любой другой тег | Контейнер под будущие виджеты |

`class` и `id` стыкуются с CSS. `module="clock"` загружает `modules/clock.csx`.

## CSS

Поддерживается узкое подмножество:

- селекторы: `.class`, `.class:hover`, `#id`, `button`
- свойства: `background-color`, `color`, `border-color`, `border-width`, `border-radius`, `padding`, `margin`, `width`, `height`, `opacity`, `font-size`, `font-weight`, `transform`, `transition`

Пример анимации кнопки:

```css
.icon-btn {
  transform: translateY(0px);
  transition: transform 150ms, background 150ms;
}
.icon-btn:hover {
  transform: translateY(-4px);
}
```

## C# скрипты (`.csx`)

Каждый файл в `config/modules/` — модуль. Имя файла без расширения = атрибут `module` в XML.

Доступен объект `Shell`:

| Метод | Действие |
|-------|----------|
| `Shell.Launch(path, args?, cwd?)` | Запуск программы / `.lnk` |
| `Shell.SendWinKey()` | Клавиша Win (меню Пуск) |
| `Shell.Exit()` | Закрыть оверлей и вернуть таскбар |
| `Shell.Log(message)` | Запись в лог |
| `Shell.Shortcuts` | Список ярлыков |

Хуки (все необязательные):

```csharp
void OnInit() { }
void OnClick() { }
string OnRender() => DateTime.Now.ToString("HH:mm"); // раз в секунду
```

Roslyn компилирует скрипты в runtime, поэтому **Native AOT отключён**. Для Debug/Release JIT это нормальный режим.

## Архитектура

```
config/  →  XML parser  →  Avalonia controls
            CSS parser  →  Avalonia styles
            CSX host    →  клики / OnRender
ShellManager            →  hide Explorer taskbar + AppBar + restore on exit
```

Отрисовка — Avalonia 11 (прозрачное окно без рамки, topmost, не в панели задач). Один и тот же `ShellManager` восстанавливает Explorer при закрытии, `ProcessExit` и необработанном исключении.

## Сборка

```powershell
dotnet build -c Release
```

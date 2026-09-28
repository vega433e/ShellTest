# ShellOverlay

Свой shell-оверлей вместо панели задач Windows. Рисуется на **Avalonia UI**, настраивается **файлами**: XML для раскладки, CSS для вида, C#-скрипты (`.csx`) для логики.

При старте программа скрывает системный таскбар, резервирует полосу снизу как AppBar и показывает свою панель.

---

## Запуск

Нужен **.NET 10 SDK** и **Windows 10 2004+ / Windows 11**.

```powershell
dotnet run -c Debug
```

Выход:
- кнопка **✕** на панели
- глобальный хоткей **Ctrl+Alt+Q** (настраивается в `config/app.json`)

Если таскбар пропал после аварийного завершения:

```powershell
Stop-Process -Name explorer -Force
Start-Process explorer
```

Логи: `config/shelloverlay.log`.

---

## Структура конфига

Все настройки — в папке `config/`. Она копируется в `bin/` при сборке.

| Файл | Назначение |
|------|------------|
| `app.json` | Глобальные настройки: что прятать, хоткей выхода, имена файлов |
| `layout.xml` | Раскладка панели |
| `styles.css` | Стили и анимации |
| `shortcuts.json` | Закреплённые ярлыки (пины) |
| `modules/*.csx` | Логика для отдельных элементов панели |

После правок — **перезапусти приложение**. Hot reload пока не поддерживается.

---

## `app.json`

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

| Поле | Что делает |
|------|------------|
| `hideTaskbar` | Скрыть основной таскбар Windows |
| `hideSecondaryTaskbars` | Скрыть таскбары на вторых мониторах |
| `hideNotifyOverflow` | Скрыть окно overflow-иконок трея |
| `registerAppBar` | Зарегистрировать панель как AppBar (резервирует место снизу) |
| `exitHotkey` | Хоткей выхода, формат `Mod+Mod+Key` (`Ctrl+Alt+Q`, `Win+Shift+F12`) |
| `layoutFile` | Имя файла раскладки |
| `stylesFile` | Имя файла стилей |
| `shortcutsFile` | Имя файла ярлыков |
| `modulesDirectory` | Папка с `.csx` модулями |

---

## `layout.xml` — раскладка

Это **свой DSL**, не Avalonia XAML. Парсер читает его и собирает контролы Avalonia.

### Общая структура

```xml
<shell>
  <bar id="main" class="bar" edge="bottom" height="44" margin="6">
    <button id="start" class="start" module="start">⊞</button>
    <separator class="divider"/>
    <pins/>
    <filler/>
    <label id="clock" class="time" module="clock">--:--</label>
    <button id="exit" class="exit" module="exit">✕</button>
  </bar>
</shell>
```

Корень — `<shell>`. Внутри — один `<bar>`, это и есть панель.

### Атрибуты `<bar>`

| Атрибут | Значения | Смысл |
|---------|----------|-------|
| `edge` | `bottom` (по умолчанию) \| `top` | К какому краю экрана крепить |
| `height` | число (DIP) | Высота панели |
| `margin` | число (DIP) | Отступ панели от края окна |
| `id` | строка | Идентификатор для CSS (`#main`) |
| `class` | строка | CSS-класс (обычно `bar`) |

### Теги внутри `<bar>`

| Тег | Что создаёт | Атрибуты |
|-----|-------------|----------|
| `<button>` | Кнопка | `module` (какой `.csx` вызвать), `launch` (путь к программе) |
| `<label>` / `<text>` / `<clock>` | Текстовый блок | `module` — если модуль умеет возвращать текст |
| `<separator>` | Вертикальная линия-разделитель | — |
| `<pins>` | Все ярлыки из `shortcuts.json` | — |
| `<panel>` | Контейнер (StackPanel) | `orientation` — `horizontal` (по умолчанию) или `vertical`, `spacing` — отступ между детьми |
| `<spacer>` | Пустое место | `width` в DIP |
| `<filler>` | **Разделитель**: всё после него уходит вправо | — |
| `<widget>` (или любой другой тег) | Заготовка под будущий виджет | `module` |

### Общие атрибуты любого тега

- `id="..."` — для CSS-селектора `#id`. Внутри преобразуется в валидный C#-идентификатор (дефисы → подчёркивания).
- `class="a b c"` — один или несколько классов. Применяются CSS-селекторы `.a`, `.b` и т.д.
- `module="name"` — подключает `modules/name.csx`. Работает для `<button>` (клик) и для `<label>` (перерисовка текста).

### Пример: как добавить ярлык прямо в разметке, без `shortcuts.json`

```xml
<button class="icon-btn" launch="notepad.exe">N</button>
```

---

## `styles.css` — стили

Поддерживается **узкое подмножество CSS**. Правила парсятся на старте.

### Селекторы

| Селектор | Что значит |
|----------|------------|
| `.class` | Все контролы с классом |
| `.class:hover` | Тот же класс в состоянии наведения |
| `#id` | Контрол с данным id |
| `button` | Все кнопки |
| `button.class` | Кнопки с этим классом |
| `button.class:hover` | То же + наведение |

Псевдокласс `:hover` работает и для `button`, и для любого класса.

### Свойства

| Свойство | Формат | Пример |
|----------|--------|--------|
| `background-color` / `background` | `#RRGGBB`, `#AARRGGBB`, `red`, `transparent` | `#1e1e2e` |
| `color` | то же | `#cdd6f4` |
| `border-color` | то же | `#45475a` |
| `border-width` | число (DIP) | `1` |
| `border-radius` | число (DIP) | `12` |
| `padding` | `N` \| `V H` \| `T R B L` | `4px 10px` |
| `margin` | то же | `6` |
| `width` / `height` | число | `32` |
| `opacity` | 0..1 | `0.9` |
| `font-size` | число | `13` |
| `font-weight` | `normal` \| `bold` \| `semibold` \| `light` \| число | `600` |
| `transform` | `translateY(Npx)` \| `translateX(Npx)` \| `scale(N)` | `translateY(-2px)` |
| `transition` | `prop Nms, prop Nms` | `background 120ms, transform 120ms` |

Единицы: `px` / `dip` пишутся или опускаются — результат один.

### Примеры

**Кнопка с hover-подъёмом:**

```css
.icon-btn {
  background-color: #313244;
  color: #cdd6f4;
  border-radius: 8px;
  transform: translateY(0px);
  transition: transform 120ms, background 120ms;
}

.icon-btn:hover {
  background-color: #45475a;
  transform: translateY(-2px);
}
```

**Панель:**

```css
.bar {
  background-color: #1a1a24;
  border-radius: 14px;
  border-width: 1px;
  border-color: #2e2e42;
  padding: 4px 10px;
}
```

---

## `shortcuts.json` — ярлыки

```json
{
  "shortcuts": [
    { "name": "Terminal", "path": "wt.exe" },
    { "name": "Explorer", "path": "explorer.exe" },
    { "name": "VS Code",  "path": "%LOCALAPPDATA%\\Programs\\Microsoft VS Code\\Code.exe" },
    { "name": "Steam",    "path": "C:\\Program Files (x86)\\Steam\\steam.exe",
      "arguments": "-silent",
      "workingDirectory": "C:\\Program Files (x86)\\Steam" }
  ]
}
```

| Поле | Обязательно | Смысл |
|------|-------------|-------|
| `name` | да | Имя. **Первая буква** используется как подпись на кнопке |
| `path` | да | Путь к `.exe`, `.lnk` или `%ENV%`-переменная |
| `arguments` | нет | Аргументы командной строки |
| `workingDirectory` | нет | Рабочая папка |

Каждому ярлыку создаётся кнопка с классом `icon-btn pin`, текст — первая буква имени. Хочешь уникальные буквы — делай имена так, чтобы первые буквы не повторялись.

---

## `modules/*.csx` — свои модули

Каждый файл в `config/modules/` — это скрипт. Имя без расширения = значение атрибута `module` в `layout.xml`.

### Доступный объект `Shell`

| Метод | Что делает |
|-------|-----------|
| `Shell.Launch(path, args?, cwd?)` | Запустить программу |
| `Shell.SendWinKey()` | Нажать клавишу Win (открыть меню Пуск Windows) |
| `Shell.Exit()` | Закрыть оверлей и вернуть таскбар |
| `Shell.Log(message)` | Записать в лог |
| `Shell.Shortcuts` | Список ярлыков из `shortcuts.json` |

### Хуки

Все необязательны.

```csharp
void OnInit()   { /* вызывается один раз при старте */ }
void OnClick()  { /* вызывается по клику */ }
string OnRender() => "…";  /* возвращает текст, обновляется ~раз в секунду */
```

### Примеры

**`modules/clock.csx`** — часы с секундами:

```csharp
string OnRender() => DateTime.Now.ToString("HH:mm:ss");
```

**`modules/start.csx`** — открыть меню Пуск Windows:

```csharp
void OnClick()
{
    Shell.Log("Opening Windows Start menu");
    Shell.SendWinKey();
}
```

**`modules/exit.csx`** — выход:

```csharp
void OnClick() => Shell.Exit();
```

**Свой модуль с логикой:**

```csharp
void OnInit() => Shell.Log("My module is ready");

void OnClick()
{
    Shell.Launch("notepad.exe");
    Shell.Log("Notepad launched");
}
```

---

## Частые задачи

### Поменять цвет панели

В `styles.css` найди блок `.bar` и измени `background-color`. Формат — `#RRGGBB`.

### Сделать панель выше

В `layout.xml` у `<bar>` параметр `height="44"` → поставь больше (например, `52`).

### Добавить ярлык

Открой `shortcuts.json`, добавь объект:

```json
{ "name": "Notepad", "path": "notepad.exe" }
```

### Убрать разделители

В `layout.xml` удали строки `<separator class="divider"/>`.

### Поменять символ на кнопке

В `layout.xml` внутри `<button>…</button>` — текст между тегами:

```xml
<button id="start" class="start" module="start">★</button>
```

### Сделать свою кнопку-запускалку без модуля

```xml
<button class="icon-btn" launch="C:\\Windows\\System32\\calc.exe">C</button>
```

---

## Troubleshooting

| Симптом | Что делать |
|---------|-----------|
| Таскбар не возвращается после выхода | `Stop-Process -Name explorer -Force; Start-Process explorer` |
| Панель пустая / белая | Смотри `config/shelloverlay.log` — там будут ошибки XML или CSS |
| CSS не применяется | Проверь, что `class="..."` в XML совпадает с селектором `.class` в CSS |
| Модуль не загрузился | Ищи в логе `Loaded module 'X'`. Если нет — синтаксическая ошибка в `.csx` |
| Второй экземпляр приложения | Single-instance: второй просто выйдет молча. Убей первый через диспетчер |
| Ошибка `0x800711C7` при запуске | Заблокировал Smart App Control. Отключи в Параметрах Windows |

---

## Архитектура (для разработчиков)

```
config/ → парсер XML → контролы Avalonia
        → парсер CSS → стили Avalonia
        → CSX-хост   → клики / OnRender
ShellManager         → скрытие таскбара + AppBar + восстановление
```

- Окно — Avalonia 11, прозрачное, без рамки, topmost, не в таскбаре.
- `ShellManager` восстанавливает Explorer при закрытии, `ProcessExit` и необработанном исключении.
- CSX-скрипты компилируются Roslyn'ом в runtime, поэтому Native AOT отключён.

## Сборка

```powershell
dotnet build -c Release
```
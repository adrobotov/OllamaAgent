# OllamaAgent — AI Чат Клиент

[![.NET](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/)
[![Avalonia](https://img.shields.io/badge/Avalonia-11.2.1-3ac79d)](https://avaloniaui.net/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

AI чат клиент с поддержкой инструментов (tool calling), работающий с локальными моделями через [Ollama](https://ollama.ai/).

## 🚀 Возможности

- **Tool Calling** — автоматическое использование инструментов (файлы, процессы, Git, веб, погода)
- **Thinking Mode** — режим размышлений для reasoning-моделей (DeepSeek R1, QwQ)
- **Vision/Multimodal** — поддержка изображений в чате
- **Markdown** — рендеринг Markdown с поддержкой таблиц, списков, цитат
- **История чатов** — сохранение и загрузка сессий
- **Тёмная/Светлая тема** — переключение в один клик
- **Поиск** — по сообщениям чата и по сессиям
- **Экспорт/Импорт** — JSON и Markdown форматы
- **Горячие клавиши** — Ctrl+N, Ctrl+D, Ctrl+S, Ctrl+Enter

## 📋 Требования

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Ollama](https://ollama.ai/) с установленной моделью (например, `ollama pull llama3.2`)

## 🛠 Сборка и запуск

### Сборка из исходников

```powershell
# Восстановить пакеты и собрать
.\build.ps1

# Или вручную
dotnet restore
dotnet build
dotnet run --project src/OllamaAgent/OllamaAgent.csproj
```

### Создание установщика

```powershell
# Собрать Release версию и создать установщик
.\build.ps1 -Configuration Release

# Требует установленный NSIS: https://nsis.sourceforge.io/Download
```

## ⚙️ Настройка

### 1. Установите Ollama

Скачайте и установите [Ollama](https://ollama.ai/) для вашей ОС.

### 2. Загрузите модель

```bash
# Рекомендуемая модель
ollama pull llama3.2

# Другие поддерживаемые модели
ollama pull mistral-large
ollama pull qwen2.5
ollama pull gemma2
```

### 3. Запустите приложение

```powershell
dotnet run --project src/OllamaAgent/OllamaAgent.csproj
```

Приложение автоматически подключится к Ollama по умолчанию `http://localhost:11434`.

### 4. Настройте в приложении

1. Нажмите **⚙ Настройки** в заголовке окна
2. Укажите URL Ollama (если отличается от default)
3. Выберите модель из списка
4. При необходимости настройте API ключ для погоды
5. Настройте системный промпт

## 🎯 Поддерживаемые модели

### Tool Calling поддерживают:
- `llama3.1`, `llama3.2`
- `gemma2`, `gemma3`, `gemma4`
- `mistral-large`
- `qwen2.5`, `qwen3`
- `codellama`, `codegemma`

### Thinking Mode поддерживают:
- `deepseek-r1`
- `qwq`, `qwq-32b`

## 🔧 Инструменты

Приложение предоставляет следующие инструменты:

| Категория | Инструменты |
|-----------|-------------|
| **Файлы** | Чтение, запись, список каталогов, создание каталогов, поиск |
| **Процессы** | Список процессов, завершение процесса, выполнение команд |
| **Git** | Статус, diff, лог, ветки, add, commit |
| **Веб** | Поиск в интернете, загрузка страниц |
| **Погода** | Получение прогноза погоды |

## ⌨️ Горячие клавиши

| Клавиша | Действие |
|---------|----------|
| `Ctrl + Enter` | Отправить сообщение |
| `Ctrl + N` | Новый чат |
| `Ctrl + D` | Удалить текущий чат |
| `Ctrl + S` | Сохранить конфигурацию |
| `Ctrl + Shift + S` | Экспорт чата в JSON |

## 📁 Структура проекта

```
OllamaAgent/
├── src/
│   └── OllamaAgent/
│       ├── Models/          # Модели данных
│       ├── ViewModels/      # View Models (MVVM)
│       ├── Views/           # XAML представления
│       ├── Services/        # OllamaClient
│       ├── Tools/           # Инструменты
│       ├── Controls/        # MarkdownTextBlock
│       ├── Converters/      # XAML конвертеры
│       └── Helpers/         # MarkdownRenderer
├── tests/                   # Unit-тесты (xUnit)
├── installer/               # NSIS скрипт
└── build.ps1               # CI скрипт
```

## 🧪 Тесты

```powershell
# Запустить все тесты
dotnet test

# Или через build script
.\build.ps1
```

## 📝 Хранение данных

- **Конфигурация**: `%UserProfile%\.ollama-agent\config.json`
- **Сессии чатов**: `{AppDirectory}\sessions\*.json`

## 🐛 Решение проблем

### Модель не найдена
Убедитесь, что Ollama запущен и модель загружена:
```bash
ollama list
```

### Ошибка подключения
Проверьте, что Ollama слушает `http://localhost:11434`:
```bash
curl http://localhost:11434/api/tags
```

### Инструменты не работают
Убедитесь, что используете поддерживаемую модель (см. список выше).

## 📄 Лицензия

MIT License

## 🤝 Вклад

Проблемы и pull requests приветствуются!

---

**Разработано с ❤️ на .NET 8 + Avalonia**

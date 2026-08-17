# CardinalDirectionGlazing

## Назначение

Revit-модуль для инсоляции, ориентации, уклонов кровли или связанных расчетов.

Тип репозитория: активный модуль RibbonCITRUS.

## Технологический стек

- C# / .NET
- TargetFramework: net8.0-windows, net48
- WPF/XAML
- Windows Forms
- Autodesk Revit API
- NuGet: System.Resources.Extensions

## Структура репозитория

- `CardinalDirectionGlazing/`
- `CardinalDirectionGlazing.Tests/`
- `data/`
- `.gitattributes`
- `.gitignore`
- `CardinalDirectionGlazing.dll`
- `CardinalDirectionGlazing.pdb`
- `CardinalDirectionGlazing.sln`
- `CardinalDirectionGlazingSettings.xml`
- `Directory.Build.props`

## Сборка и запуск

Основная точка сборки: `CardinalDirectionGlazing.sln`.

```powershell
dotnet build .\CardinalDirectionGlazing.sln
```

Для Revit-плагинов может потребоваться сборка в конфигурации целевой версии Revit (`R20xx`) через Visual Studio/MSBuild.

## Тесты

Найдены тестовые проекты:
- `CardinalDirectionGlazing.Tests/CardinalDirectionGlazing.Tests.csproj`

Обычно запуск:

```powershell
dotnet test
```

## Интеграции и зависимости

- Модуль относится к активной линейке RibbonCITRUS и обычно подключается через общий Revit ribbon-host.

## Важные ограничения

- Специальные ограничения не выявлены автоматически; перед изменениями свериться с владельцем проекта и кодом.

## Статус документации

Документация сформирована автоматически 2026-06-24 по локальной структуре репозитория, проектным файлам и существующим Markdown-документам. Если назначение описано предположительно, перед разработкой нужно сверить его с владельцем проекта или исходным кодом.


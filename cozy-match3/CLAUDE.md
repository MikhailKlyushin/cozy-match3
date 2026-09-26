# Match-3 WebGL — правила проекта

Unity 6000.3.21f1 · WebGL · URP · uGUI · Zenject · UniTask · R3 1.3.1 (NuGet core) · DOTween ·
New Input System · Unity Test Framework.

## Читать перед работой

| Документ | Что в нём |
|---|---|
| `docs/gdd-match3.md` | **дизайн-контракт.** Решения `D01`–`D15`, стадии RESOLVE (§5.1), крайние случаи `E01`–`E24` — зафиксированы. Код, противоречащий им, — дефект, а не вариант |
| `docs/architecture.md` | **где именно в коде** это живёт: сборки, типы, границы, запреты |
| `docs/tasks.md` | план разработки, карточки задач T01–T26, Definition of Done |

Приоритет при конфликте: GDD > architecture.md > привычки.
Отклонение допустимо только вместе с правкой соответствующего документа в том же изменении.

`.plans/` не используется — план живёт в `docs/tasks.md`.

## Жёсткие запреты (блокеры на ревью)

- **Потоки.** `Task.Run`, `UniTask.RunOnThreadPool`, `SwitchToThreadPool`, `new Thread`,
  `Parallel.*` — WebGL однопоточный, это либо исключение, либо зависшая вкладка.
- **Блокировки на async.** `.Wait()`, `.Result`, `.GetAwaiter().GetResult()` — вкладка умирает
  навсегда, не на кадр.
- **Недетерминизм в геймплее.** `UnityEngine.Random`, `Guid.NewGuid()`, `DateTime.Now`, обход
  `Dictionary`/`HashSet`. Один инжектируемый `IRandom` на попытку уровня, порядок потребления —
  §13 GDD (`D12`).
- **Legacy input.** `Input.*`, `GetAxis`, `GetKey` — `activeInputHandler: 1`, они молча не сработают.
- **R3 time-операторы в геймплее.** `Observable.Timer/Interval/Delay/Debounce/Throttle/EveryUpdate/
  ObserveOnFrame` — установлен только NuGet-core R3: таймеры идут по настенным часам, frame-провайдер
  бросает исключение. Время — только UniTask (`DelayType.DeltaTime`) или DOTween.
- **`Modules` → `Features`** в ссылках `.asmdef`, циклы, рантайм → Editor.
- **`switch` по типу препятствия** внутри `DAMAGE`/`CLEAR`/`GRAVITY` — расширяемость обеспечивается
  данными по осям §7.1.
- **View читает или мутирует `Board` во время проигрывания хода** (правило V1, `docs/architecture.md` §6).
- **Читы вне `#if MATCH3_CHEATS`** и чит-ассеты в `Assets/Resources`.
- **LINQ, замыкания, конкатенация строк, `Instantiate`** в горячем пути (расчёт хода, каскад,
  per-cell код). Фишки, препятствия и FX — только из пулов.

## Обязательное

- Публичные async-методы принимают и уважают `CancellationToken`.
- R3-подписки — в `CompositeDisposable`, освобождаются в `Dispose`/`OnDestroy`. `AddTo(gameObject)`
  в этой сборке R3 **не существует**.
- DOTween-твины — `SetLink(gameObject)` или явный `Kill()`.
- `PlayerPrefs.Save()` после каждой записи прогресса (в WebGL это IndexedDB).
- Ядро правил (`Assets/Game/Scripts/Modules/*`) тестируется edit-mode тестами без сцены. Правило,
  проверяемое только запуском Play, — Major-находка.
- `internal` по умолчанию в Modules, `public` — только контракт.
- Именование: `PascalCase` типы и публичные члены, `_camelCase` приватные поля, `I*` интерфейсы,
  `Can…`/`Is…`/`Has…` для bool-методов, события `<Field>Changed`, обработчики `On<Event>`.
- Порядок членов: ctor-инжектированные readonly зависимости → поля → консты → свойства → события →
  публичные методы → приватные методы.
- Ассеты: `T_*_2D` текстуры, `VFX_*`, `VAR_*` варианты префабов, `SHG_*` шейдерграфы.
- **Закрыть Unity Editor перед коммитом** — иначе теряются кэшированные изменения.
- Ветки: `feature/<snake_case>`. В описании PR: что сделано, какие `D`/`E`/`§` реализованы, тесты,
  чем проверять (сцена, уровень, seed, чит), gif/видео для визуального.

## Самопроверка перед сдачей задачи

Прогнать `/match3-code-review` по своему диффу и устранить блокеры и Major до передачи на ревью.

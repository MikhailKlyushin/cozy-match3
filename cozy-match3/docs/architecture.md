# Match-3 — Архитектура реализации (контракт для агентов)

| | |
|---|---|
| Версия | 1.0 |
| Дата | 2026-09-09 |
| Статус | контракт |
| Основание | `docs/gdd-match3.md` (дизайн), `docs/Technical Task.md` (приёмка), `.claude/skills/match3-code-review` (стандарты ICVR + ревью) |
| Стек | Unity 6000.3.21f1 WebGL, URP, uGUI, Zenject, UniTask, R3 1.3.1 (NuGet core), DOTween, New Input System, Unity Test Framework |
| Аудитория | агенты-исполнители задач из `docs/tasks.md` |

---

## 0. Как читать этот документ

Три источника истины, и они не взаимозаменяемы:

1. **`docs/gdd-match3.md`** — *что* должно происходить. Решения `D01`–`D15`, стадии RESOLVE (§5.1),
   крайние случаи `E01`–`E24` — **зафиксированы**. Код, противоречащий им, — дефект, а не вариант.
2. **`docs/Technical Task.md`** — критерии приёмки заказчика.
3. **Этот документ** — *где именно в коде* это живёт: сборки, типы, границы, запреты. Всё, что
   здесь названо (namespace, имя типа, имя события транскрипта), — контракт между параллельно
   работающими агентами. Переименование в одиночку ломает чужую задачу.

Если реализация требует отклонения — сначала правка этого документа (или GDD) в том же PR, с
причиной. Молчаливое расхождение = блокер на ревью.

**Приоритет при конфликте:** GDD > Technical Task > этот документ > привычки исполнителя.

---

## 1. Инварианты

Нарушение любого пункта — блокер на ревью, без обсуждения (источник: `match3-code-review`).

| # | Инвариант | Как обеспечивается архитектурно |
|---|---|---|
| I1 | Ядро правил тестируется **без Unity и без сцены** | Все `Modules/*` (кроме `Levels.Authoring`) имеют `"noEngineReferences": true` — `UnityEngine.Random`, `Debug.Log`, `Time.time` там просто не компилируются |
| I2 | Никаких потоков | WebGL однопоточный. `Task.Run`, `UniTask.RunOnThreadPool`, `SwitchToThreadPool`, `new Thread`, `Parallel.*` запрещены везде |
| I3 | Никаких блокировок на async | `.Wait()`, `.Result`, `.GetAwaiter().GetResult()` вешают вкладку браузера навсегда |
| I4 | Детерминизм | Один `IRandom` на попытку уровня, инжектится. `UnityEngine.Random`, `Guid.NewGuid()`, `DateTime.Now`, обход `Dictionary`/`HashSet` в геймплее — запрещены (`D12`, §13) |
| I5 | Cancellation | Каждый публичный async-метод принимает `CancellationToken` и уважает его. Тир-даун уровня посреди каскада не должен оставить продолжения, пишущие в мёртвое поле |
| I6 | Явные времена жизни | R3-подписки — в `CompositeDisposable`, освобождаются в `Dispose`/`OnDestroy`. DOTween-твины — `SetLink(gameObject)` или `Kill()` |
| I7 | Только New Input System | `activeInputHandler: 1`. Legacy `Input.*` компилируется и молча не работает |
| I8 | `Modules` не ссылаются на `Features` | Проверяется в `.asmdef` в обе стороны. Циклы запрещены |
| I9 | Читы не попадают в релиз | Сборка `Match3.Cheats` с `defineConstraints: ["MATCH3_CHEATS"]`, префаб панели не должен ссылаться из всегда-загружаемых ассетов (§12 GDD) |
| I10 | Модель — источник истины, view её не правит | View **никогда** не мутирует поле и **не читает** его во время проигрывания хода (см. §6, правило V1) |

---

## 2. Ключевые архитектурные решения

Решения этого документа. Каждое — с ценой, которую мы платим осознанно.

| # | Решение | Почему | Цена |
|---|---|---|---|
| **A01** | **Ход вычисляется синхронно целиком и отдаётся презентации как «транскрипт» — упорядоченный список событий.** Модель не ждёт анимаций | Требования GDD (детерминизм §13, «ANIMATE до GRAVITY» §5.1, каскады, цепочки волнами) выполняются структурно, а не дисциплиной. Тест «тот же seed + те же вводы ⇒ та же партия» становится сравнением хешей. Ядру не нужны ни UniTask, ни R3, ни Unity — отсюда `noEngineReferences` (I1) | Во время проигрывания модель «впереди» картинки. Отсюда правило V1 (§6) и полная блокировка ввода на проигрывании (совпадает с `D05`/`E17`) |
| **A02** | Поле ≤ 9×9, каскад ≤ 30, волн ≤ 20 — синхронный расчёт хода умещается в один кадр | Дешевле любой асинхронной машины состояний | Нужен кап-контроль (он и так требуется `D08`) |
| **A03** | `##` (неразрушимый блокер) — **не отдельный тип клетки**, а `FieldElement` с осями `damageSource=None, health=Infinite, goalRole=NotCountable` | §7.1 требует расширяемости через данные. Так исчезает целый класс частных случаев в гравитации и уроне | `CellKind` остаётся только `Playable`/`Hole` |
| **A04** | Препятствия описываются **данными** (`ElementDefinition` + `ElementId` из реестра), а не enum-ом типов. Логика урона — стратегии по оси `damageSource` | Прямое требование заказчика («инфраструктура должна быть расширяемой») и §7.1. `switch` по типу ящика внутри `DAMAGE`/`CLEAR` — антипаттерн, названный в ревью-скиле | Косвенность: токен → `ElementId` → `ElementDefinition` |
| **A05** | Конфиг уровня разделён: `LevelConfig` (ScriptableObject, авторинг) → `LevelData` (чистый DTO) → `Board` | Тесты строят уровень из строки-раскладки без ассетов; парсер и валидатор — чистые. Соответствует роли **Data** в стандартах ICVR («биндим Data, а не ScriptableObject») | Один лишний слой преобразования |
| **A06** | `Match3.Levels` **не** ссылается на `Match3.Resolve`; `SwapValidator` и `LegalMoveService` живут в `Match3.Matching` | Иначе цикл сборок: генерация начального поля (`E20`) требует проверки легального хода, а RESOLVE требует конфига | — |
| **A07** | Наведение (`D09`) — **один** сервис `TargetingService`, единственная реализация, инжектится и в самолётик, и во все комбинации с радужным шаром | Два похожих правила разъезжаются при первой правке (прямая цитата `D09`) | — |
| **A08** | Время жизни попытки уровня — префаб `LevelContext` с Zenject `GameObjectContext` | Уничтожение объекта закрывает R3-подписки, твины и сабконтейнер разом; «перезапуск уровня» = пересоздание контекста. Ни одного per-level объекта в project scope | Модель живёт в контейнере GameObject-а, хотя сама Unity не требует |
| **A09** | R3 — **только для распространения состояния** (`ReactiveProperty`, `Subject` состояния UI). Любой **таймер** — UniTask или DOTween | Установлен только NuGet-core R3: `DefaultTimeProvider` = настенные часы (игнорирует `timeScale` и паузу), `DefaultFrameProvider` бросает исключение в рантайме. `Observable.Timer/Interval/Delay/Debounce/EveryUpdate` в геймплее — находка на ревью | Таймер подсказки (§5.5) пишется на `UniTask.Delay(..., DelayType.DeltaTime, ct)` |
| **A10** | Любая мутация поля — **только через `TurnRule`**, и она всегда возвращает транскрипт. Читы в том числе | Один путь синхронизации модель↔view. Второй путь («чит правит поле напрямую») — источник рассинхрона, который ловится последним | Чит-команды оформляются как явные операции хода |
| **A11** | Презентация проигрывает транскрипт через реестр `ITurnEventPlayer` по видам событий | Новое событие = новый плеер, без правки центрального `switch`. Симметрично A04 на стороне view | Реестр надо один раз собрать в инсталлере |
| **A12** | Визуальный стиль (котики) живёт **только** в профилях-ассетах: индекс цвета → спрайт/форма/FX. В домене цвет — индекс `ChipColor`, и ничего больше | Арт меняется без правки правил; палитра и формы настраиваются под цветовую слепоту (§17) | — |

---

## 3. Карта сборок

```
Assets/Game/Scripts/
  Modules/                                  КОД ТОЛЬКО. Никаких сцен, префабов, .asset
    Match3.Core/                            noEngineReferences
    Match3.Board/                           noEngineReferences
    Match3.Matching/                        noEngineReferences
    Match3.Goals/                           noEngineReferences
    Match3.Boosters/                        noEngineReferences
    Match3.Resolve/                         noEngineReferences
    Match3.Levels/                          noEngineReferences
    Match3.Levels.Authoring/                UnityEngine (ScriptableObject-типы)
      Editor/  Match3.Levels.Authoring.Editor   includePlatforms: [Editor]
  Content/      Match3.Content/              ScriptableObject-профили презентации, см. ниже
  Features/
    Match3.Diagnostics/
    Match3.Gameplay/
    Match3.Hud/
    Match3.Progression/
    Match3.Cheats/                          defineConstraints: [MATCH3_CHEATS]
  Bootstrap/    Match3.Bootstrap/            Zenject-инсталлеры, композиция
  EditorTools/  Match3.EditorTools/          includePlatforms: [Editor], см. §18
Assets/Game/Tests/
  EditMode/  Match3.Tests.EditMode/
  PlayMode/  Match3.Tests.PlayMode/
```

### 3.1 Граф ссылок (ацикличный, проверяется на ревью)

| Сборка | Ссылается на |
|---|---|
| `Match3.Core` | — |
| `Match3.Board` | Core |
| `Match3.Matching` | Core, Board |
| `Match3.Goals` | Core, Board |
| `Match3.Boosters` | Core, Board, Goals |
| `Match3.Resolve` | Core, Board, Matching, Goals, Boosters |
| `Match3.Levels` | Core, Board, Goals, Matching |
| `Match3.Levels.Authoring` | Core, Board, Goals, Levels |
| `Match3.Levels.Authoring.Editor` | + Levels.Authoring |
| `Match3.Content` | Core, Board |
| `Match3.Diagnostics` | Core, Goals, Resolve, Zenject |
| `Match3.Gameplay` | Core, Board, Content, Matching, Goals, Boosters, Resolve, Levels, Diagnostics, Zenject, UniTask, R3, DOTween(+Modules), Unity.InputSystem |
| `Match3.Hud` | Core, Content, Goals, Resolve, Levels, Zenject, UniTask, R3, DOTween(+Modules) |
| `Match3.Progression` | Core, Content, Goals, Levels, Levels.Authoring, Resolve, Diagnostics, Zenject, UniTask, R3 |
| `Match3.Cheats` | все Modules + Gameplay (только интерфейсы), Hud, Progression, Zenject, UniTask, R3 |
| `Match3.Bootstrap` | все Modules + все Features + Zenject |
| `Match3.Tests.EditMode` | все Modules + Levels.Authoring + Content + Diagnostics + Progression + Gameplay + Hud + Zenject, UniTask, nunit |
| `Match3.Tests.PlayMode` | + Features, Bootstrap |

**Почему тестовая edit-mode сборка ссылается и на фичи.** Изначально в таблице стояло «все Modules
+ Levels.Authoring + nunit», но карточки T17/T19/T20/T21 требуют edit-mode тестов на **чистые
части** презентации и прогрессии: расписание тика целей, профиль ускорения падения, геометрия
свайпа, поток уровней на подменяемом хранилище. Правило «правило, проверяемое только запуском
Play, — Major-находка» (§19) сильнее, чем узкий список ссылок, поэтому ссылки добавлены. Условие
остаётся прежним: в edit-mode тестируется то, что не требует сцены; всё остальное — play-mode
smoke (T23).

Запрещено: `Modules` → `Features` (в любом виде), `Hud` → `Gameplay`, `Gameplay` → `Hud`,
`Gameplay` → `Cheats`, циклы, рантайм-сборка → Editor-сборка.

`Match3.Cheats` → `Match3.Gameplay` разрешено **только на интерфейсы**, объявленные в Gameplay
(`IBoardCellPicker`, `IBoardOverlayView`). Правило ICVR «контракт объявляет поставщик» применяется
и к фичам.

### 3.2 Ловушки этого проекта (проверено на текущем состоянии репозитория)

- **DOTween лежит в `Assets/Plugins/Demigiant/DOTween/` как DLL плюс `Modules/*.cs` без `.asmdef`.**
  Скрипты из `Plugins/` попадают в предопределённую сборку `Assembly-CSharp-firstpass`, а её
  **невозможно** указать в ссылках `.asmdef`. Следствие: `DOTweenModuleUI` (`DOFade` у `Graphic`,
  `DOAnchorPos`, `DOColor` у `Image`) и `DOTweenModuleSprite` из фич-сборок недоступны.
  **Обязательный шаг T02:** `Tools > Demigiant > DOTween Utility Panel > Create ASMDEF`.
- **Zenject** свой `.asmdef` имеет (`Assets/Plugins/Zenject/zenject.asmdef`) — ссылаться можно.
- **R3** — только NuGet-core (`Assets/Packages/R3.1.3.1/lib/netstandard2.1/R3.dll`), пакета
  `com.cysharp.r3` в манифесте **нет**. Значит нет `AddTo(gameObject)`, `UnityTimeProvider`,
  `UnityFrameProvider`, `ObservableTracker`, `.OnUpdate()`. См. A09.
- **Addressables отсутствуют.** Загрузка — прямые ссылки или `Assets/Resources`. Помнить: всё из
  `Resources` попадает в WebGL-билд независимо от использования и дефайнов.
- `scriptingDefineSymbols` сейчас пуст — `MATCH3_CHEATS` надо прописать (T02).
- `Assets/TutorialInfo`, `Assets/Readme.asset`, `Assets/Scenes/SampleScene.unity` — мусор шаблона,
  удаляется в T02.

---

## 4. Слои и роли

Роли — из стандартов ICVR. Класс, не попадающий ни в одну роль, скорее всего смешивает
ответственности.

| Роль | Что можно | Где живёт | Примеры |
|---|---|---|---|
| **Model** | Хранит состояние. Без Unity. Без анимаций | Modules | `Board`, `ElementInstance`, `GoalState` |
| **Data (DTO)** | Неизменяемый payload инициализации | Modules | `LevelData`, `GoalDefinition`, `ElementDefinition` |
| **Config** | ScriptableObject для авторинга; конвертируется в Data | Levels.Authoring | `LevelConfig`, `LevelCatalog`, `ElementDefinitionAsset` |
| **Service** | Алгоритм, юнит-тестируемый; состояние хода — только в переданном контексте | Modules | `MatchDetectionService`, `GravityService`, `TargetingService` |
| **Rule** | Оркестрация сценария; командует сервисами | Modules / Features | `TurnRule`, `LevelFlowRule` |
| **Factory** | **Только** фабрики и пулы создают объекты | Features | `ChipViewFactory`, `LevelSessionFactory` |
| **Presenter** | Подписан на модель/транскрипт, командует View. Модель не мутирует | Features | `MovesCounterPresenter`, `GoalsPanelPresenter` |
| **View** | Только отображение; ввод отдаёт наружу как события | Features | `ChipView`, `BoardView`, `CheatPanelView` |
| **Repository** | Персистентность | Features | `ProgressRepository` |

Запрещено: классы `*Controller`, `*Manager` без роли, статические `Instance`, сервис-локаторы,
инжект `DiContainer` в геймплей-код, `new`/`Instantiate` вне фабрик и пулов.

---

## 5. Модель домена

Namespace = имя сборки. `internal` по умолчанию, `public` — только контракт.

### 5.1 `Match3.Core` — словарь

```csharp
public readonly struct GridPos : IEquatable<GridPos> { public readonly int X, Y; }   // (0,0) = левый-низ (D01)
public enum ChipColor : byte { None = 0, C1 = 1, C2, C3, C4, C5, C6 }               // индекс, не цвет
public enum BoosterType : byte { None = 0, RocketH, RocketV, Bomb, Rainbow, Airplane }
public enum CellKind : byte { Hole = 0, Playable = 1 }                              // A03
public enum SlotKind : byte { Empty = 0, Chip, Booster }
public readonly struct ElementId : IEquatable<ElementId> { public readonly ushort Value; }

public interface IRandom {
    int Seed { get; }
    int NextInt(int maxExclusive);
    int NextInt(int minInclusive, int maxExclusive);
    void Shuffle<T>(IList<T> list);          // Fisher–Yates, §5.4
}
public interface IMatch3Logger { void Info(string m); void Warn(string m); void Error(string m); }
```

Плюс: `DeterministicRandom : IRandom` (над `System.Random`), `NullLogger`, `Grid<T>` (плоский
массив с индексатором по `GridPos`), `PooledList<T>`, `CellBuffer`.

**Порядок потребления RNG зафиксирован (`D12`, §13)** и является частью контракта:
генерация начального поля → цвета из спавнеров → фаза `cx` → тай-брейки наведения →
перемешивание → бонус остатка ходов. Новое потребление добавляется **в конец** списка и
фиксируется здесь же.

### 5.2 `Match3.Board` — состояние поля

```csharp
public struct ChipSlot   { SlotKind Kind; ChipColor Color; BoosterType Booster; int InstanceId; bool Consumed; }
public struct Cell       { CellKind Kind; ChipSlot Slot; int ElementIndex; }          // -1 = нет элемента
public sealed class ElementInstance {
    ElementId Definition; int Health; ChipColor CurrentColor; int NestedIndex; int ImmuneUntilStep;
}
public sealed class Board : IBoardReader { /* мутирующее API — internal, только для Resolve/Levels */ }

public interface IBoardReader {                          // для matching/boosters/view/тестов
    int Width { get; } int Height { get; }
    ElementCatalog Catalog { get; }                        // оси элемента читаются данными, не switch-ем
    bool Contains(GridPos p);
    CellKind GetKind(GridPos p);
    ChipSlot GetSlot(GridPos p);
    bool TryGetElement(GridPos p, out ElementInstance e);
    bool IsPassableForFall(GridPos p);                    // §5.3, E10
    bool IsMovable(GridPos p);
    bool IsSpawnerColumn(int x);                           // §3.3
    ulong ComputeHash();                                  // тесты детерминизма
}
```

**Уточнения контракта, внесённые при реализации T03:**

- `Catalog`, `Contains`, `IsSpawnerColumn` добавлены в `IBoardReader`. Без `Catalog` любой
  потребитель (гравитация, урон, наведение) вынужден был бы получать оси элемента отдельным
  каналом или через `switch` по токену — то есть ровно через то, что §10 запрещает.
- `ComputeHash()` хеширует **только состояние правил**: вид клетки, слот (вид/цвет/бустер),
  определение элемента, hp, текущий цвет и вложенную цепочку. `InstanceId` (визуальная
  идентичность) и транзитный флаг `Consumed` в хеш **не входят**: два поля с одинаковыми
  цветами в одинаковых клетках — это одно и то же игровое состояние.
- `BuiltInElementCatalog` живёт в **`Match3.Board`**, а не в `Match3.Levels` (как значилось в
  таблице ниже). Каталог §7.2 — это данные о элементах; в `Match3.Levels` он заставлял бы тесты
  самого `Match3.Board` зависеть от модуля уровней, которого на момент T03 ещё нет.
- Мутирующее API `Board` — `internal` + `InternalsVisibleTo` для `Match3.Matching` (пробный свап
  в `SwapValidator` делается свапом с откатом), `Match3.Resolve`, `Match3.Levels` и тестовых
  сборок.

`ChipSlot.InstanceId` — детерминированный счётчик (не `Guid`): по нему view сопоставляет фишку и
её визуал при проигрывании транскрипта.

`ElementDefinition` — оси §7.1 как данные (A04):

```csharp
public enum DamageSourceKind : byte { AdjacentMatch, AdjacentMatchOfColor, OnCell, BoosterOnly, None }
public enum ElementColorMode  : byte { None, Fixed, Cycling }
public enum Occupancy         : byte { OccupiesCell, UnderChip, OverChip }
public enum GravityBehaviour  : byte { StaticBlocksFall, StaticPassable, Falls }
public enum SpreadBehaviour   : byte { None, EveryNMoves }
public enum GoalRole          : byte { Countable, NotCountable }
public enum PerTurnBehaviour  : byte { None, CycleColor }

public sealed class ElementDefinition {
    ElementId Id; string Token;                     // "bx", "b2", "c3", "cx", "##"
    DamageSourceKind DamageSource; ElementColorMode ColorMode; ChipColor FixedColor;
    int MaxHealth;                                  // -1 = Infinite
    Occupancy Occupancy; GravityBehaviour Gravity; SpreadBehaviour Spread;
    GoalRole GoalRole; PerTurnBehaviour PerTurn;
    bool IsColoredBox;                              // для цели DestroyAnyColoredBox
}
public sealed class ElementCatalog { bool TryResolve(string token, out ElementDefinition def); }
```

Каталог §7.2 целиком выражается этими данными: `c1`–`c6` = `ColorMode.Fixed`, `cx` =
`ColorMode.Cycling + PerTurn.CycleColor`, `##` = `DamageSource.None + MaxHealth = -1 +
NotCountable`. `b2`/`b3` отличаются от `bx` **только** `MaxHealth`. Если новая механика того же
класса требует правки пайплайна — задача решена неверно (§10).

### 5.3 Публичная поверхность остальных модулей

| Сборка | Публичные типы (контракт) |
|---|---|
| `Match3.Matching` | `MatchComponent` (цвет, клетки, ранг, клетка появления, бустер), `MatchDetectionService`, `SwapValidator`, `LegalMoveService` (+ кэш), `ComponentClassifier`, `SpawnCellResolver`, `MatchFreePlacement` (подбор цвета без готового матча: `E20` и третья фаза §5.4) |
| `Match3.Goals` | `GoalDefinition`, `GoalState`, `IGoalTracker` (`CreditChip`, `CreditElement`, `CreditBoosterActivation`, `AllClosed`, `FirstUnclosedIndex`), `GoalTracker` |
| `Match3.Boosters` | `IBoosterEffect`, `RocketEffect`/`BombEffect`/`RainbowEffect`/`AirplaneEffect`, `BoosterCatalog`, `ITargetingService`/`TargetingService`, `ComboMatrix`, `ComboPlan`, `ComboResolver` |
| `Match3.Resolve` | `TurnRule`, `TurnTranscript`, `TurnEvent`, `ResolveLoopService`, стадийные сервисы (`BoosterSpawnService`, `ActivationService`, `DamageService`, `ClearService`, `GravityService`, `RefillService`), `ShuffleService`, `HintService`, `MovesBonusService`, `IChipSpawnPolicy`, `ResolveCaps` |
| `Match3.Levels` | `LevelData`, `LayoutParser`, `TokenGrid`, `LevelValidator`, `ValidationReport`, `BoardBuilder`, `FlowReachabilityAnalyzer`, `MoveLimitCalculator` |
| `Match3.Board` | `Cell`, `ChipSlot`, `ElementInstance`, `Board`, `IBoardReader`, `ElementDefinition`, `ElementCatalog`, `BuiltInElementCatalog`, `ElementTokens` |
| `Match3.Levels.Authoring` | `LevelConfig`, `LevelCatalog`, `ElementDefinitionAsset`, `LevelConfigConverter` |

---

## 6. Транскрипт хода — центральный контракт

Единственный канал «модель → презентация». Тот, кто пишет транскрипт (`Match3.Resolve`), и тот,
кто его проигрывает (`Match3.Gameplay`, `Match3.Hud`), разрабатываются параллельно — поэтому схема
здесь контракт, а не рекомендация.

```csharp
public enum TurnEventKind : byte {
    TurnBegin, SwapRejected, SwapPerformed, MoveCharged,
    StepBegin,                       // Step = номер шага разрешения (каскада), с 0
    BoosterSpawned,                  // D06: помечен, не сработал
    BoosterActivated,                // Cell, Booster, Wave, Source(player|chain|combo|cheat|bonus)
    ComboActivated,                  // A, B, Booster(=A), BoosterB; эпицентр = A
    BoosterEffectCells,              // клетки, задетые текущей активацией (для FX)
    ChipDestroyed,                   // Cell, Color
    ChipTransformed,                 // Cell, Booster — масс-превращение (§6.3)
    ElementDamaged,                  // Cell, Amount, Value = остаток hp
    ElementDestroyed,                // Cell, Element
    ElementRevealed,                 // Cell, Element — вложенное (§7.1)
    ElementColorCycled,              // Cell, Color — POST-TURN, шаг 1
    GoalProgress,                    // Amount = дельта, Value = новое значение, InstanceId = индекс цели
    AnimateBarrier,                  // ← стадия 7: FX уничтожения должны догореть ДО падения
    ChipMoved,                       // A→B, Flags = Fall|Slide|Swap|Shuffle
    ChipSpawned,                     // Cell, Color; стартует из-за верхнего края
    StepEnd,                         // Step, Value = depth
    WaveBarrier,                     // пауза 0.12 с между волнами (§6.4)
    PostTurnBegin, ShuffleBegin, ShuffleEnd,
    MovesBonusRocket,                // Cell, Booster
    LevelWon, LevelLost,             // Flags = moves|deadlock|cheat
    CapHit,                          // Flags = WaveCap|DepthCap — всегда баг (E05, §14)
    TurnEnd
}

public readonly struct TurnEvent {
    TurnEventKind Kind; byte Step, Wave, Flags;
    GridPos A, B;
    ChipColor Color; BoosterType Booster; ElementId Element;
    int Amount, Value, InstanceId;
    int CellsOffset, CellsCount;                    // срез в TurnTranscript.Cells
}

public sealed class TurnTranscript {
    IReadOnlyList<TurnEvent> Events { get; }
    IReadOnlyList<GridPos> Cells { get; }            // общий буфер для срезов
    TurnOutcome Outcome { get; }                     // Rejected | Resolved | Won | Lost
    int MovesLeft { get; } int MaxDepth { get; } int Seed { get; }
    void Reset();                                    // пулится, не аллоцируется на каждый ход
}
```

**Правила транскрипта (нарушение = блокер):**

- **T1.** Порядок событий = порядок стадий §5.1. `AnimateBarrier` пишется между `CLEAR` и
  `GRAVITY` каждого шага — именно он материализует требование заказчика «опадание не начинается,
  пока не завершились эффекты уничтожения».
- **T2.** `BoosterSpawned` в каскаде **не** сопровождается `BoosterActivated` в том же шаге (`D06`).
- **T3.** Каждая волна ACTIVATE: список `BoosterActivated` + их `BoosterEffectCells`, затем
  `WaveBarrier` (§6.4, пауза 0.12 с).
- **T4.** `GoalProgress` пишется в момент зачёта (CLEAR / активация бустера), с дельтой — HUD
  тикает счётчик по событию, а не пересчитывает состояние (`E18`: `Value` уже заклампован).
- **T5.** Транскрипт **самодостаточен**: по нему одному можно нарисовать ход, ни разу не спросив
  `Board`. Это и есть правило V1.
- **T6.** `MoveCharged` — сразу после `SwapPerformed` (`D04`), до любого `StepBegin`.
- **T7.** Транскрипт пулится (`Reset()`), `Cells` — общий буфер. Аллокации на ход стремятся к нулю.

**Правило V1 (презентация).** Во время проигрывания транскрипта view **не читает** `Board`: модель
уже в финальном состоянии, а картинка — в прошлом. View держит своё визуальное состояние (карта
`InstanceId → ChipView`). Чтение `Board` из плеера или презентера — блокер. Легальные исключения:
**построение** сцены уровня (начальная раскладка) и оверлеи читов.

---

## 7. Пайплайн: стадия → код

Полный ход: `IDLE → VALIDATE → COMMIT → RESOLVE (цикл) → POST-TURN → IDLE` (§5.1).
Оркестратор — `TurnRule` (`Match3.Resolve`), единственная точка входа (A10):

```csharp
public sealed class TurnRule {
    TurnTranscript ExecuteSwap(GridPos a, GridPos b);
    TurnTranscript ExecuteTap(GridPos p);
    TurnTranscript ExecuteCheat(in CheatCommand cmd);   // A10: чит — тоже ход
    HintPlan GetHint();                                  // §5.5, детерминированно
    bool CanAcceptInput { get; }                         // D05
}
```

| Стадия §5.1 | Класс | Ключевые контракты |
|---|---|---|
| VALIDATE | `SwapValidator` (Matching) | легален, если обе клетки играемы и подвижны И (свап создаёт матч ИЛИ хотя бы в одной клетке бустер). Нелегален → `SwapRejected`, ход **не** списывается (`E16`) |
| COMMIT | `TurnRule` | `SwapPerformed` + `MoveCharged` здесь (`D04`, `E15`) |
| 1 DETECT | `MatchDetectionService` | примитивы (линия ≥3, квадрат 2×2) → union-find по общим клеткам → компоненты (`D02`) |
| 2 CLASSIFY | `ComponentClassifier`, `SpawnCellResolver`, `MatchFreePlacement` (подбор цвета без готового матча: `E20` и третья фаза §5.4) | ранги 1–5 (§4.2), клетка появления (§4.3); в каскаде пункт 1 правила пропускается (`E03`) |
| 3 SPAWN | `BoosterSpawnService` | помечает, не активирует (`D06`) |
| 4 ACTIVATE | `ActivationService` + `BoosterCatalog` + `ComboResolver` | **волнами**: сначала собрать список, потом стрелять. Флаг `consumed`, кап 20 волн (`D08`, `E05`) |
| 5 DAMAGE | `DamageService` + стратегии по `DamageSourceKind` | 1 урон на **источник** за шаг (`D07`), overkill отбрасывается, `##` не получает урона и эффект не тормозит |
| 6 CLEAR | `ClearService` | удаление, зачёт целей, раскрытие вложенного с иммунитетом до конца шага (§7.1) |
| 7 ANIMATE | — | в модели это `AnimateBarrier` в транскрипте (T1) |
| 8 GRAVITY | `GravityService` | проходами; обход пустых `y↑, x↑`; вертикаль, затем диагонали — **левая раньше правой** (§5.3, `D10`) |
| 9 REFILL | `RefillService` + `IChipSpawnPolicy` | спавнеры (§3.3), веса `spawnWeights` |
| 10 STABLE? | `ResolveLoopService` | новые матчи → шаг 1, `depth += 1`; кап 30 → `CapHit` + `logger.Error` |
| POST-TURN | `TurnRule` | строго: `CycleColor` (`cx`) → **цели** → ходы → легальные ходы/перемешивание. Победа приоритетнее поражения (`D14`, `E13`/`E14`) |
| Бонус | `MovesBonusService` | каждый остаток → ракета, шаг 0.15 с, кап 10; провалить уровень не может (`E23`, §8.4) |
| Перемешивание | `ShuffleService` | Fisher–Yates из RNG попытки (10 попыток) → регенерация цветов (10 попыток) → детерминированная расстановка без матчей + ремонтный свап; только потом `deadlock` + `logger.Error` (`E19`, §5.4) |
| Подсказка | `HintService` | приоритеты 1–4 (§5.5), тай-брейки `y↑, x↑`, горизонталь раньше вертикали; «бустер+бустер» исключены (`D15`) |

**Раскладка файлов в `Match3.Resolve`.** Над этой сборкой работают сразу несколько агентов
(T09–T13), поэтому файлы разнесены по папкам: `Stages/` (по **одному файлу на сервис стадии**),
`Turn/` (`TurnRule`, `PostTurn`), `Transcript/`, `Hint/`, `Shuffle/`, `Bonus/`. Один сервис — один
файл; общий «god-файл» пайплайна запрещён не только стилистически, но и потому, что превращает
параллельную работу в конфликты слияния.

**Источник урона** (`D07`) — это `readonly struct DamageSource { SourceKind Kind; int Id; }`, где
`Id` — индекс матч-компонента или индекс активации бустера. Дедупликация — по **источнику**, а не
по клетке: площадной урон, накрывший ящик тремя клетками, — это 1 урон (`E09`).

**Кэш легальных ходов.** Строится один раз на POST-TURN, используется и перемешиванием (§5.4), и
подсказкой (§5.5). Инвалидируется любым изменением поля. Второго прохода по полю нет.

---

## 8. Детерминизм

Требование §13: одинаковые seed + одинаковая последовательность ввода ⇒ одинаковая партия.

- Один `IRandom` на **попытку** уровня, создаётся в `LevelInstaller`, инжектится всем.
  Проигранный уровень переигрывается с **новым** seed (§8.3).
- Порядок потребителей — §5.1 этого документа (он же `D12`). В dev-билдах
  `RecordingRandom : IRandom` пишет последовательность вызовов в лог: расхождение порядка ловится
  сразу, а не через «уровень непроходим и невоспроизводим».
- Никаких `Dictionary`/`HashSet` в геймплейном обходе. Где нужна множественность — сортированные
  массивы или битовые маски по `Grid<T>`. Порядок обхода везде явный: `y↑, x↑`.
- `Board.ComputeHash()` — основа теста реплея: лог ввода + seed → хеш поля после каждого хода.

---

## 9. Уровни

`LevelConfig` (SO) → `LevelConfigConverter` → `LevelData` (DTO) → `BoardBuilder` → `Board` (A05).

```csharp
public sealed class LevelData {            // чистый DTO: строится и из SO, и из строки в тестах
    int Id, Width, Height, ColorCount, MoveLimit;
    DifficultyTier Tier;
    IReadOnlyList<GoalDefinition> Goals;   // порядок = отображение И приоритет наведения (§8.2)
    IReadOnlyList<string> LayoutRows;      // первая строка = y = height − 1 (D01)
    IReadOnlyList<NestedContent> Contents; // {x, y, token}, внешний → внутренний
    IReadOnlyList<int> Spawners;           // пусто = дефолт §3.3
    IReadOnlyList<ColorWeight> SpawnWeights;
    float HintDelaySeconds;                // 0 = выключено
}
```

- **Флип строк делается ровно один раз**, в `LayoutParser` (`D01`). Второй флип во view невидимо
  отменяет первый — классический баг этого проекта; проверяется тестом на всех 12 уровнях.
- Токен = **2 символа**, разделитель — пробел (§10.2). Вложенность в сетке не пишется — только в
  `contents`.
- `LevelValidator` (§3.4): достижимость каждой играемой клетки от спавнера по правилам §5.3
  (непроходимы только `__` и `##`), отсутствие готовых матчей, наличие легального хода,
  достижимость целей, глубина вложенности ≤ 3, `colorCount ∈ [4,6]`, соответствие размеров,
  неизвестные токены. В редакторе — ошибка, в билде — лог. Кривой уровень падает громко на
  загрузке, а не даёт «слегка сломанное» поле.
- `MoveLimitCalculator` (§9.3) — не рантайм-логика, а инструмент валидации: сверяет `moveLimit`
  конфига с формулой и предупреждает о расхождении. Уровень 11 — зафиксированное отклонение (+1).
- **Последний уровень = максимальный `id` среди загруженных конфигов.** Литерал `12` в коде —
  находка на ревью (§8.3).

---

## 10. Расширяемость препятствий

Прямое требование заказчика. Проверка архитектуры: **добавление льда/цепи/лозы не должно менять
ни одной строки в `Match3.Resolve`.**

Как добавляется новое препятствие:

1. Новый `ElementDefinitionAsset` (или строка в `BuiltInElementCatalog`) со значениями по осям §7.1.
2. Новый токен — через реестр каталога, а не через `switch` в парсере.
3. Визуальный профиль (`ElementVisualProfile`) + префаб/спрайты.

Когда правка кода **всё-таки** нужна: если механика требует нового **значения оси** (например
`Occupancy.UnderChip`, объявленного, но не реализованного) — добавляется новая стратегия,
регистрируемая в реестре:

```csharp
public interface IDamageSourceRule { DamageSourceKind Kind { get; } bool IsDamagedBy(in DamageContext ctx); }
// AdjacentMatchRule, AdjacentMatchOfColorRule, OnCellRule, BoosterOnlyRule, ImmuneRule
```

`DamageService` перебирает не типы ящиков, а правила из реестра по значению оси. **`switch` по
токену или типу препятствия внутри `DAMAGE`, `CLEAR` или `GRAVITY` — Major-находка минимум.**

Оси `spread`, `UnderChip`/`OverChip`, `Falls` объявлены в модели и не используются в 12 уровнях —
это осознанно (§7.1): их наличие определяет, придётся ли переписывать пайплайн потом.

---

## 11. Презентация

### 11.1 Состав `Match3.Gameplay`

| Класс | Ответственность |
|---|---|
| `BoardView` | Корень поля, маска, карта `InstanceId → ChipView`, построение начальной раскладки |
| `BoardLayout` | `cellSize = min(area/width, area/height)` (§13), пересчёт при смене разрешения — **не кэшировать один раз** |
| `ChipView`, `ElementView`, `BoosterView` | Отображение одной сущности. Только визуал |
| `ViewPool<T>`, `ChipViewFactory` | Пулинг (§13). Ни одного `Instantiate` в каскаде |
| `TranscriptPlayer` | Проигрывает `TurnTranscript` как `UniTask` с `CancellationToken`; соблюдает барьеры T1/T3 |
| `ITurnEventPlayer` + реестр | По одному плееру на вид события (A11) |
| `FxRegistry`, `CameraShake` | Пулы FX-префабов, тряска по §11.3 |
| `BoardInputPresenter` | New Input System: drag-свап и тап; ввод заблокирован, пока идёт проигрывание (`D05`) |
| `HintPresenter`, `HintView` | Таймер простоя на `UniTask.Delay(..., DelayType.DeltaTime, ct)` (A09), пульсация 3 цикла + повтор (§11.3) |
| `IBoardCellPicker` | Интерфейс для читов: «дай клетку по тапу» |

### 11.2 Тайминги

Все значения §11.3 — в одном ассете `TimingProfile` (ScriptableObject,
`Assets/Game/Content/Gameplay/Configs/`). Магические числа в коде анимаций — Minor-находка. Профиль
читает только презентация: модель о времени не знает вообще (A01).

### 11.3 uGUI

- Референс 1080×1920, Canvas Scaler *Scale With Screen Size*, match 0.5 (§13).
- Диапазон 9:16 … 16:9: поле центрируется в квадратной области, HUD перекомпонуется над/под ним.
  Захардкоженные пиксельные отступы, ломающиеся вне референсного аспекта, — находка.
- **Страница фиксирует 9:16** (леттербокс, §13 GDD), поэтому в шипнутой сборке канвас-юниты всегда
  ровно `1080 × 1920`. Это не отменяет предыдущий пункт: код обязан держать диапазон и покрыт
  приёмкой, потому что леттербокс — решение WebGL-шаблона, а не движка, и меняется одной правкой
  CSS. Кадр, шаблон и потолок буфера — `docs/art-direction.md` §3.1.
- `Raycast Target` выключен у всех неинтерактивных график. Никаких `SetActive`-дёрганий и
  перестроений layout во время каскада — только пул и смена позиций.

---

## 12. DI (Zenject) и времена жизни

| Скоуп | Контекст | Что живёт |
|---|---|---|
| Project | префаб `ProjectContext` | `IMatch3Logger`, `ProgressRepository`, `LevelCatalog`, `ElementCatalog`, `TimingProfile`, `PopupService`, `MetricsReporter` |
| Scene | `SceneContext` в `Game.unity` | `LevelFlowRule`, презентеры HUD, `CheatsInstaller` (под дефайном) |
| **Level attempt** | префаб `LevelContext` с `GameObjectContext` (A08) | `IRandom` (seed попытки), `Board`, `GoalTracker`, все сервисы `Match3.Resolve`, `TurnRule`, `BoardView`, пулы фишек и FX |

- Перезапуск уровня = `Destroy(levelContext)` + инстанс нового. Подписки R3, твины и пулы уходят
  вместе с ним. Ни одного per-level объекта в project scope.
- Конструкторная инъекция по умолчанию; `[Inject]`-метод — только во `MonoBehaviour`-view.
- Сервисы: `BindInterfacesAndSelfTo<T>().AsSingle()` в своём скоупе. Сервис не резолвит view.
- `DiContainer` в геймплей-код не инжектится (сервис-локатор в маскировке).

**Обязательный bootstrap-шаг (A09):** в `ProjectInstaller` подписать
`ObservableSystem.RegisterUnhandledExceptionHandler(...)` на логгер, иначе исключение в R3-цепочке
исчезает молча.

---

## 13. Async и Reactive

**UniTask — всё, что связано со временем и последовательностями:** проигрывание транскрипта,
таймер подсказки, попапы, WIN/LOSE-последовательности.

- Публичный async-метод принимает `CancellationToken` (I5). Токен уровня — из
  `CancellationTokenSource`, отменяемого при тир-дауне `LevelContext`.
- `.Forget()` — только как осознанный адаптер, который **логирует** исключение. Молчаливый
  `.Forget()` на проигрывании хода съедает исключение, объясняющее зависшее поле.
- Задержки: `UniTask.Delay(ms, DelayType.DeltaTime, PlayerLoopTiming.Update, ct)`.
  `DelayType.Realtime` игнорирует паузу.
- Потоков нет (I2). Блокировок нет (I3).

**R3 — только состояние:**

- `ReactiveProperty<int> MovesLeft`, `ReactiveProperty<GoalState>`, `Subject<LevelResult>`.
- Наружу — `Observable<T>` / `ReadOnlyReactiveProperty<T>`. Публичный `Subject`-сеттер — находка.
- Подписки — в `CompositeDisposable`, освобождаются в `OnDestroy`/`Dispose`. `AddTo(gameObject)`
  в этой сборке R3 **не существует**.
- **Запрещены в геймплее:** `Observable.Timer`, `Interval`, `Delay`, `Debounce`, `Throttle`,
  `EveryUpdate`, `ObserveOnFrame`, `Sample` — по A09 они либо игнорируют игровое время, либо
  бросают в рантайме.
- Один потребитель и никакой композиции → обычное событие или прямой вызов. `Subject` на каждое
  поле — шум.

---

## 14. WebGL: бюджеты и запреты

| Параметр | Цель |
|---|---|
| FPS | 60 на десктопном Chrome/Edge, 30 — допустимый минимум (§13) |
| Аллокации на ход | ~0 в стабильном состоянии: транскрипт и буферы пулятся, view — из пула |
| Размер билда | Держать под контролем; ни одного лишнего ассета в `Resources` |

Запрещено в горячем пути (расчёт хода, проигрывание, per-cell код):

- LINQ, замыкания с захватом, `params`-массивы, боксинг, конкатенация строк (включая аргументы
  `Debug.Log`, которые собираются даже при выключенном логировании), `foreach` по интерфейсным
  коллекциям там, где важна аллокация энумератора.
- `Instantiate`/`Destroy` — только через фабрики и пулы.

Память: `webGLMemorySize: 32` + `MemoryGrowthMode: 2` — растёт по необходимости, но всплеск GC на
каскаде виден как фриз. Пулинг — не оптимизация «на потом», а требование §13.

---

## 15. Читы и дефайны

- `MATCH3_CHEATS` добавляется в `Player Settings > Scripting Define Symbols` (Editor +
  Development-конфигурация).
- `Match3.Cheats` имеет `"defineConstraints": ["MATCH3_CHEATS"]` — без дефайна сборка **не
  компилируется вовсе**, а не «компилируется и не используется».
- Ссылка на префаб панели — в поле, обёрнутом `#if MATCH3_CHEATS`, на `CheatsInstaller`. Префаб
  **не** кладётся в `Assets/Resources` (оттуда он попадёт в релизный билд независимо от дефайнов).
  Проверка обязательна: релизный WebGL-билд + Build Report на отсутствие чит-ассетов (T25).
- Панель — **только экранные элементы, никаких горячих клавиш** (требование задания).
- Полный список элементов — §12 GDD. Первые три («перейти на уровень», «выиграть», «проиграть») —
  прямые требования `Technical Task.md`: отсутствие любого = провал приёмки, а не Minor.
- Все чит-команды, **меняющие поле**, идут через `TurnRule.ExecuteCheat` (A10) и возвращают
  транскрипт: `WinLevel`, `LoseLevel`, `AddMoves`, `PlaceBooster`, `FreeMoves`
  (`CheatCommand.IsBoardCommand`). `GoToLevel` и `SetSeed` поле не мутируют, а **пересоздают
  попытку уровня**, поэтому презентер читов направляет их в `LevelFlowRule`. Это не ослабление
  A10: инвариант A10 — «никто не правит поле в обход `TurnRule`», а пересоздание уровня строит
  поле заново через `BoardBuilder`. Словарь команд при этом один — `CheatCommand`.

---

## 16. Метрики (§14)

`MetricsReporter` (`Match3.Diagnostics`) подписан на транскрипты и события уровня, пишет в консоль
и лог структурированные события: `level_start`, `level_end`, `booster_fired`, `shuffle_triggered`,
`cascade_depth`, `wave_cap_hit`, `hint_shown`, `hint_followed`. Схема полей — из §14 без изменений:
она совпадает с продуктовой.

`wave_cap_hit` и `deadlock` — **всегда баг**, логируются через `logger.Error`.

---

## 17. Визуальный стиль: кошачья комната

Стиль — уютная кошачья комната. Правило одно: **стиль не проникает в правила** (A12).

- В домене цвет — индекс `ChipColor` (1…6), и только он. Ни одного «оранжевого кота» в модулях
  `Match3.*`.
- `ChipVisualProfile` (SO, `Assets/Game/Content/Gameplay/Configs/`): `ChipColor` → спрайт
  **предмета кошачьего быта** (клубок, мышь, миска, подушка, лапка, колокольчик), силуэт, цвет
  частиц, префаб FX уничтожения, опциональная idle-анимация (только визуал, без влияния на
  тайминги §11.3).
- **Доступность:** индексы различаются **силуэтом предмета**, оттенок — вторичный признак.
  Шесть пастельных объектов одной светлоты нечитаемы, а метрика «доля ходов после подсказки > 30 %»
  (§14) правится именно артом. Между соседними индексами — светлотная лестница ≥ 8 L\*
  (`docs/art-direction.md` §2.2).
- Кот присутствует как **маскот**: спящий кот на фоне комнаты и в реакциях на победу и поражение.
  Фишками коты не являются — шесть морд различаются аксессуарами внутри одного силуэта, шесть
  предметов различаются силуэтом целиком, и второе выдерживает проверку обесцвечиванием.
- Препятствия в этом языке: `bx`/`b2`/`b3` — картонные коробки (кот в коробке), состояния hp —
  прогрессивно помятая коробка (визуальное состояние **обязано** меняться на каждый снятый hp,
  §7.1); `c1`–`c6` — коробка с бантом цвета N; `cx` — коробка с меняющим цвет бантом плюс пип
  следующего цвета (§7.2, Q5 = включить); `##` — переноска или когтеточка, очевидно неразрушимая.
- Бустеры: ракета — игрушка-ракета / лазерная указка, бомба — клубок-бомба, радужный шар —
  радужный клубок, самолётик — бумажный самолётик с ушами.
- Именование ассетов (стандарты ICVR): `T_Chip_Cat01_2D`, `VFX_ChipDestroy_Cat01`,
  `VAR_ChipView_Cat01`. Один атлас на фишки, ≤ 2048, сжатие под WebGL — милый арт легко раздувает
  билд.
- Ассеты одной фичи лежат вместе (§18), чтобы удаление фичи не превращалось в охоту по проекту.

**Арт-дирекшн, полный реестр ассетов и промпты генерации — `docs/art-direction.md`.** Там же
палитра, светлотная лестница фишек и ограничения рантайма для VFX (overlay-канвас не рисует
`ParticleSystem`, один FX-префаб обслуживает все роли эффекта).

**Пошаговая инструкция по подмене арта и подключению FX-префабов — `docs/art-and-fx-guide.md`.**
Там же зафиксирована ловушка: `Match3/Authoring/Generate All` перезаписывает спрайты-заглушки и
обнуляет ссылки на FX в профилях.

---

## 18. Структура `Assets`

```
Assets/
  Game/                                                    ВСЁ игровое — здесь
    Scripts/      Modules/ · Features/ · Bootstrap/ · EditorTools/   (см. §3)
    Tests/        EditMode/ · PlayMode/
    Content/                                               контент по фичам
      Gameplay/   Art/ · Prefabs/ · FX/ · Configs/
      Hud/        Art/ · Prefabs/ · Configs/
      Cheats/     Prefabs/
      Levels/     Level01.asset … Level12.asset · LevelCatalog.asset · Elements/
    Scenes/       Boot.unity · Game.unity  (+ папки метаданных сцен)
  Settings/       URP-ассеты (есть)
  Plugins/        Zenject · Demigiant (есть)
  Packages/       NuGet: R3 и зависимости (есть)
  Resources/      только DOTweenSettings. Графику и чит-префабы сюда НЕ кладём
```

**Единый корень `Assets/Game/`** — требование заказчика: всё, что является игрой (код, контент,
сцены, тесты), лежит под одной папкой. Снаружи остаётся только то, что игрой не является и чей
путь диктуется третьей стороной: `Plugins/` (Zenject, DOTween), `Packages/` (NuGet-восстановление),
`Resources/DOTweenSettings` (DOTween ищет ассет именно там), `Settings/` (URP).

**Осознанное отклонение от буквы стандарта ICVR:** стандарт помещает контент фичи внутрь папки
фичи (`Scripts/Features/<Name>/`). Мы держим код в `Assets/Game/Scripts/**`, а контент — в
`Assets/Game/Content/<Feature>/`, сохраняя *смысл* правила (самодостаточная папка на фичу) и не
смешивая `.cs` с артом. Отклонение зафиксировано здесь, чтобы ревью не считало его случайным.

**`Match3.EditorTools`** (`Assets/Game/Scripts/EditorTools/`, `includePlatforms: [Editor]`) — сборка
инструментов авторинга: генерация ассетов уровней, профилей и префабов, сборка сцен, headless-билд.
Её нет в §3, потому что она не участвует в рантайме; добавлена, поскольку сцены, префабы и
`.asset`-и в этом проекте создаются скриптами, а не руками в редакторе. Ссылаться на неё из
рантайм-сборок запрещено (правило «рантайм → Editor»).

**`Match3.Content`** (`Assets/Game/Scripts/Content/`) — типы ScriptableObject-профилей презентации:
`TimingProfile` (§11.2), `ChipVisualProfile`, `ElementVisualProfile` (§17). Появилась потому, что
§3.1 не даёт `Match3.Gameplay` и `Match3.Hud` **ни одной общей сборки с `UnityEngine`**, а профили
нужны обоим: тайминги §11.3 читают и плеер транскрипта, и тик счётчика целей; спрайты фишек нужны
и полю, и иконкам целей в HUD. Ссылка `Hud → Gameplay` запрещена, `Match3.Levels` —
`noEngineReferences` и `ScriptableObject` держать не может. Сборка содержит **только данные**:
ни презентеров, ни view, ни логики правил.

Префабы: в `Prefabs/` — только варианты (`VAR_*`) и сборные view-префабы фичи.

---

## 19. Тестирование

Пирамида: почти всё — edit-mode над чистыми модулями; play-mode — один smoke.

**Обязательно покрыто edit-mode тестами (иначе задача не закрыта):**

| Область | Что именно |
|---|---|
| Матчи | компоненты через общую клетку (`D02`), 2×3 → самолётик, линия 4 + линия 3 → бомба, линия 6 → радужный шар, два бустера за ход (`E01`), клетка появления (§4.3, `E03`) |
| Гравитация | вертикаль, диагональ, **левая раньше правой**, проходы до стабилизации, живое препятствие блокирует (`E10`), мёртвое — нет |
| Урон | 1 на источник (`D07`), два источника = 2 (`E11`), площадной по нескольким клеткам = 1 (`E09`), overkill отбрасывается, `##` не получает урона |
| Вложенность | раскрытие на CLEAR, иммунитет до конца шага, глубина 3, зачёт цели вложенным элементом |
| Бустеры | эффект каждого из четырёх, ракета проходит поле насквозь (`D11`, `E08`), fallback радужного шара без фишек (`E06`/`E07`) |
| Комбинации | **все 10 клеток матрицы §6.3**, эпицентр = целевая клетка свапа, кап 8 самолётиков, чередование ориентации ракет |
| Цепочки | волны собираются до выстрела, `consumed` (`D08`), кап 20 волн → `CapHit` |
| Цели | все 4 типа, клампинг (`E18`), `ActivateBooster` считает срабатывания, комбинация = по одной активации каждого участника |
| Цикл хода | `D04` (ход на COMMIT), `E16` (нелегальный свап без списания), `D06`, `D14`/`E13`/`E14`, кап каскада 30 |
| POST-TURN | порядок `cx` → цели → ходы → перемешивание; `cx` меняет цвет каждый ход; урон считается по цвету на момент DAMAGE |
| Перемешивание | 10 + 10 попыток, препятствия не двигаются, `deadlock` как ошибка (`E19`) |
| Подсказка | приоритеты 1–4, исключение «бустер+бустер», тай-брейки, `hintDelaySeconds = 0` (`D15`) |
| Уровни | парсер: флип строк `D01` **ровно один раз**, 2-символьные токены, ошибки формата; валидатор на всех 12 конфигах; `E20` (нет автоматчей, есть легальный ход, 50 попыток + ремонтный проход) |
| Детерминизм | реплей лога ввода при том же seed → идентичные хеши поля после каждого хода |
| Бонус | остаток ходов → ракеты, кап 10, уровень не проваливается (`E23`) |

**Play-mode smoke:** загрузка `Boot` → `Game`, уровень 1 строится, скриптованный свап
проигрывается без исключений, попап победы по читу открывается.

Тестовые уровни собираются из строковых раскладок через `TestLevel.From(@"...")` — без
ScriptableObject и без сцены. Если правило проверяется только запуском Play — это Major-находка.

---

## 20. Трассируемость: требование → место в коде

| Требование (`Technical Task.md`) | Где реализуется |
|---|---|
| Основная механика (поле, фишки, матчи, опадание) | `Match3.Board`, `Match3.Matching`, `GravityService`, `RefillService` |
| Автоматчи при опадании | `ResolveLoopService` (шаг 10 → шаг 1, каскад) |
| Начальная раскладка без автоматчей | `BoardBuilder` + `E20` (`Match3.Levels`) |
| Эффект падения | `TranscriptPlayer` + `ChipMoved`/`ChipSpawned`, §11.3 |
| Эффект удаления; опадание **после** эффектов | `AnimateBarrier` (T1) + `TranscriptPlayer` |
| Четыре бустера | `Match3.Boosters` (`RocketEffect`, `BombEffect`, `RainbowEffect`, `AirplaneEffect`) |
| Все комбинации бустеров | `ComboMatrix` + `ComboResolver` (10 клеток, §6.3) |
| Цепочка взрывов | `ActivationService` (волны, §6.4) |
| «Умное» наведение самолётика | `TargetingService` (`D09`, §6.1) |
| Ящики + расширяемая инфраструктура | `ElementDefinition` (оси §7.1), `IDamageSourceRule`, §10 |
| Ящик с несколькими жизнями / вложенный | `ElementInstance.Health` / `NestedIndex` + `ClearService` |
| Цели уровня, несколько на уровне | `Match3.Goals` (`GoalTracker`, порядок = приоритет) |
| Переход на следующий уровень / переигровка | `LevelFlowRule` + `ProgressRepository` (`Match3.Progression`) |
| Параметры в конфиге уровня | `LevelConfig` → `LevelData` (§9) |
| Минимум 10 уровней (в наборе 12) | `Assets/Game/Content/Levels/Level01…12` + `LevelCatalog` |
| Карта уровня (цвет / случайная фишка / ящик / бустер) | `LayoutParser` + токены §10.2 |
| Читы: уровень / победа / поражение (+ остальные) | `Match3.Cheats`, §15 |

---

## 21. Риски и осознанные отклонения

| Риск | Митигация |
|---|---|
| **A01 непривычен**: модель впереди картинки, «почему поле уже другое» | Правило V1 сформулировано жёстко; dev-чит «дамп транскрипта в лог» делает ход читаемым; ввод на проигрывании заблокирован (`D05` этого и требует) |
| Рассинхрон схемы транскрипта между агентом модели и агентом view | Схема — в §6 этого документа; задача T01 фиксирует её кодом до старта параллельных задач; изменение схемы = правка §6 в том же PR |
| Тайминги §11.3 разъезжаются с событиями модели | Все длительности — в одном `TimingProfile`; тики целей идут от `GoalProgress`, а не от пересчёта состояния |
| DOTween-модули недоступны из `.asmdef` | T02: `Create ASMDEF` в DOTween Utility Panel — до старта задач презентации |
| R3 без Unity-интеграции: молчаливые настенные таймеры | A09 (запрет time-операторов) + `UnhandledExceptionHandler` в bootstrap |
| Чит-ассеты в релизном билде | `defineConstraints` + `#if`-обёрнутое поле + **проверка Build Report** в T25 |
| Милый арт раздувает WebGL-билд | Один атлас фишек ≤ 2048, сжатие, бюджет проверяется в T25 |
| 12 уровней «непроходимы при идеальной игре» (§9.3, шаг 4) | T26: жадный бот прогоняет N seed-ов на уровень и сверяет winrate с полосами §9.1 |

`.plans/` в этом проекте не используется как источник истины — план разработки живёт в
`docs/tasks.md`.

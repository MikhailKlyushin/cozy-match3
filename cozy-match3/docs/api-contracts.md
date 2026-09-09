# Match-3 — API-контракты параллельных задач W1

| | |
|---|---|
| Версия | 1.0 |
| Статус | контракт |
| Основание | `docs/architecture.md` §5–§7, `docs/gdd-match3.md`, `docs/tasks.md` |
| Зачем | задачи T04–T13 пишутся параллельно и вызывают друг друга. Здесь зафиксированы **точные** сигнатуры на границах задач. Отклонение = сломанная чужая задача |

Правила:

- Сигнатуры ниже **обязательны**. Внутреннее устройство сервиса — на усмотрение исполнителя.
- Всё, чего здесь нет, — внутренняя деталь: делай `internal`.
- Буферы (`PooledList<T>`, `CellBuffer`) передаются вызывающим и **дополняются**, не очищаются:
  очистка — ответственность владельца. Это то, что даёт «ноль аллокаций на ход» (§14).
- Порядок обхода поля всюду `y↑, x↑` (`GridPos.CompareYThenX`). `Dictionary`/`HashSet`
  в геймплейном обходе запрещены (`I4`).
- `IRandom` потребляется в порядке `RandomConsumer` (§13, `D12`).

Уже реализовано и доступно: `Match3.Core` (T01), типы транскрипта в `Match3.Resolve` (T01),
`Match3.Board` (T03). Хелпер тестов: `Match3.Tests.EditMode.Support.BoardFixture.From(layout)` —
строит `Board` из строковой раскладки (первая строка = верхняя, `D01`).

---

## T05 — `Match3.Matching`

```csharp
namespace Match3.Matching;

/// GDD §4.2. Числовые значения = ранг из GDD: 1 высший.
public enum ComponentRank : byte { None = 0, Rainbow = 1, Bomb = 2, Airplane = 3, Rocket = 4 }

/// Компонент — связная группа примитивов одного цвета (D02). Один компонент → максимум
/// один бустер; чистятся ВСЕ клетки компонента.
public sealed class MatchComponent
{
    public ChipColor Color { get; }
    public IReadOnlyList<GridPos> Cells { get; }     // отсортированы y↑, x↑
    public ComponentRank Rank { get; }
    public GridPos SpawnCell { get; }                // §4.3; GridPos.Invalid при Rank == None
    public BoosterType Booster { get; }              // None при Rank == None
    public int Index { get; }                        // порядок обнаружения; = DamageSource.Id
}

public sealed class MatchDetectionService
{
    public MatchDetectionService(IBoardReader board);

    /// Находит все компоненты. result ОЧИЩАЕТСЯ внутри.
    /// playerSwapTarget — клетка, В которую игрок сделал свап (правило 1 §4.3);
    /// GridPos.Invalid в каскаде (E03).
    public void Detect(PooledList<MatchComponent> result, GridPos playerSwapTarget);

    /// Быстрая проверка «есть ли хоть один матч» — для SwapValidator и генерации поля.
    public bool HasAnyMatch();
}

public readonly struct LegalMove
{
    public readonly GridPos A;
    public readonly GridPos B;
    public LegalMove(GridPos a, GridPos b);
}

/// VALIDATE (§5.1): свап легален, если обе клетки играемы и подвижны И
/// (свап создаёт матч ИЛИ хотя бы в одной клетке бустер).
public sealed class SwapValidator
{
    public SwapValidator(Board board, MatchDetectionService detection);
    public bool IsLegal(GridPos a, GridPos b);

    /// true, если свап создаёт матч (без учёта бустеров) — нужно подсказке (§5.5, приоритет 1).
    public bool CreatesMatch(GridPos a, GridPos b);
}

/// Кэш легальных ходов: строится один раз на POST-TURN, используется перемешиванием (§5.4)
/// и подсказкой (§5.5). Второго прохода по полю нет.
public sealed class LegalMoveService
{
    public LegalMoveService(Board board, SwapValidator validator);
    public IReadOnlyList<LegalMove> Moves { get; }   // ленивое построение, порядок y↑, x↑,
                                                     // горизонтальный свап раньше вертикального
    public bool HasAnyMove { get; }
    public void Invalidate();                        // при любом изменении поля
}
```

Заметки, которые легко нарушить:

- Примитивы: **максимальная** линия ≥3 (гор./верт.) и квадрат 2×2. Слияние в компонент — по
  общей клетке, транзитивно (union-find). Жадное присвоение фишек фигурам — **неверный
  алгоритм** (`D02`).
- Ранг 2 (бомба) — две **пересекающиеся** линии, каждая ≥3, суммарно ≥5 клеток.
  Линия 4 + пересекающая линия 3 = бомба, не ракета.
- Ориентация ракеты — из линии, давшей ранг 4, не из направления свайпа.
- Клетка появления, §4.3, первое подходящее: (1) клетка свапа, если принадлежит компоненту;
  (2) ранг 2 → пересечение линий, тай-брейк мин. `y`, затем мин. `x`; (3) ранг 3 → левая-нижняя
  клетка квадрата; (4) медиана по `(y↑, x↑)`, индекс `floor((n−1)/2)`.
  Конфликт двух компонентов за одну клетку → второй по обходу использует правило 4.
- Бустер в слоте участвует в матче как фишка? **Нет**: бустер бесцветен, в примитивы не входит.

---

## T06 — `Match3.Goals`

```csharp
namespace Match3.Goals;

public enum GoalType : byte { CollectColor = 0, DestroyElement = 1, DestroyAnyColoredBox = 2, ActivateBooster = 3 }

public readonly struct GoalDefinition
{
    public readonly GoalType Type;
    public readonly int Target;
    public readonly ChipColor Color;      // CollectColor
    public readonly string Token;         // DestroyElement
    public readonly BoosterType Booster;  // ActivateBooster

    public static GoalDefinition CollectColor(ChipColor color, int target);
    public static GoalDefinition DestroyElement(string token, int target);
    public static GoalDefinition DestroyAnyColoredBox(int target);
    public static GoalDefinition ActivateBooster(BoosterType booster, int target);
}

public readonly struct GoalState
{
    public readonly int Value;
    public readonly bool Closed;
}

/// Одна единица прогресса. Один зачёт может двинуть НЕСКОЛЬКО целей: уничтоженный c1
/// закрывает и DestroyElement("c1"), и DestroyAnyColoredBox.
public readonly struct GoalDelta
{
    public readonly int GoalIndex;
    public readonly int Delta;      // уже с учётом клампа (E18): может быть 0
    public readonly int NewValue;
}

public interface IGoalTracker
{
    int Count { get; }
    GoalDefinition GetDefinition(int index);
    GoalState GetState(int index);
    bool AllClosed { get; }

    /// Порядок конфига = приоритет наведения (§8.2). Сортировки нет. -1, если всё закрыто.
    int FirstUnclosedIndex { get; }

    /// Все методы ДОПОЛНЯЮТ deltas, не очищая. Зачёт сверх таргета отбрасывается (E18).
    void CreditChip(ChipColor color, PooledList<GoalDelta> deltas);

    /// Только при hp = 0 — частичный урон в цель не идёт. NotCountable игнорируется.
    void CreditElement(ElementDefinition definition, PooledList<GoalDelta> deltas);

    /// СРАБАТЫВАНИЯ, не создания (Q1). Цепные и комбо-срабатывания идут наравне.
    void CreditBoosterActivation(BoosterType booster, PooledList<GoalDelta> deltas);

    /// Чит «Выиграть уровень» (§12): закрывает все цели на таргете.
    void CloseAll(PooledList<GoalDelta> deltas);
}

public sealed class GoalTracker : IGoalTracker
{
    public GoalTracker(IReadOnlyList<GoalDefinition> definitions);
}
```

---

## T09 — гравитация и refill (`Match3.Resolve`, папка `Stages/`)

Файлы: `Stages/GravityService.cs`, `Stages/RefillService.cs`, `Stages/IChipSpawnPolicy.cs`,
`Stages/WeightedChipSpawnPolicy.cs`. Цикл «гравитация + refill до стабилизации» собирает
**T11**, не эта задача.

```csharp
namespace Match3.Resolve;

public interface IChipSpawnPolicy
{
    /// Цвет новой фишки для столбца x. Потребляет RNG (RandomConsumer.SpawnerColor).
    ChipColor NextColor(int x);
}

public sealed class WeightedChipSpawnPolicy : IChipSpawnPolicy
{
    /// weights == null или пустой → равномерно по colorCount (§4.1).
    public WeightedChipSpawnPolicy(IRandom random, int colorCount, IReadOnlyList<ColorWeight> weights);
}

public sealed class GravityService
{
    public GravityService(Board board);

    /// Один проход: пустые клетки в порядке y↑, x↑, для каждой — правила §5.3
    /// (1) вертикаль, (2) диагональ верх-левая, затем верх-правая. Возвращает число
    /// перемещений; пишет ChipMoved с Fall / Slide.
    public int RunSinglePass(TranscriptWriter writer);

    /// Проходы, пока за проход происходит хотя бы одно перемещение.
    public int RunUntilStable(TranscriptWriter writer);
}

public sealed class RefillService
{
    public RefillService(Board board, IChipSpawnPolicy policy);

    /// Заполняет пустые ВЕРХНИЕ клетки столбцов-спавнеров. Возвращает число новых фишек;
    /// пишет ChipSpawned. Обход столбцов x↑.
    public int Run(TranscriptWriter writer);
}
```

Что легко нарушить:

- **Порядок диагоналей: верх-левая раньше верх-правой** (`D10`) — часть контракта, не вкус.
- По диагонали соскальзывает только фишка, которая **сама не может упасть вертикально**
  (её нижняя клетка непроходима или занята).
- Диагональ проверяется, только если клетка **прямо над** пустой — непроходима
  (`__`, блокер, живое препятствие). Если над пустой пусто — это случай (1) для следующего прохода.
- Падение блокируют `__`, блокер и любое живое препятствие (`E10`). Мёртвое препятствие
  уже снято на CLEAR → падение через его клетку разрешено в том же шаге.
- Бустеры падают наравне с фишками. `Board.MoveSlot` сохраняет `InstanceId` — идентичность
  фишки для view.

---

## T10 — урон, clear, вложенность (`Match3.Resolve`, папка `Stages/`)

Файлы: `Stages/DamageService.cs`, `Stages/ClearService.cs`, `Stages/DamageSource.cs`,
`Stages/IDamageSourceRule.cs`, `Stages/DamageRules.cs`.

```csharp
namespace Match3.Resolve;

public enum DamageOriginKind : byte { MatchComponent = 0, BoosterActivation = 1 }

/// Дедупликация урона идёт ПО ИСТОЧНИКУ, а не по клетке: площадной урон, накрывший ящик
/// тремя клетками, — это 1 урон (D07, E09). Два разных источника в шаге — 2 урона (E11).
public readonly struct DamageSource
{
    public readonly DamageOriginKind Kind;
    public readonly int Id;      // индекс матч-компонента либо индекс активации бустера
    public DamageSource(DamageOriginKind kind, int id);
}

public readonly struct DamageContext
{
    public readonly IBoardReader Board;
    public readonly GridPos Cell;
    public readonly ElementInstance Element;
    public readonly ElementDefinition Definition;
    public readonly DamageSource Source;

    /// Цвет матча-источника; ChipColor.None для бустера — бустер бесцветен (Q4).
    public readonly ChipColor SourceColor;

    /// true, если источник — бустер (площадной урон).
    public readonly bool IsBoosterDamage;

    /// true, если клетка препятствия НАКРЫТА источником, а не соседствует с ним.
    public readonly bool IsOnElementCell;
}

public interface IDamageSourceRule
{
    DamageSourceKind Kind { get; }
    bool IsDamagedBy(in DamageContext ctx);
}

/// Реализации: AdjacentMatchRule, AdjacentMatchOfColorRule, OnCellRule, BoosterOnlyRule,
/// ImmuneRule (Kind == DamageSourceKind.None). Регистрируются в реестре по значению оси.
/// switch по токену или типу препятствия внутри DAMAGE/CLEAR — блокер на ревью (§10).
public static class DamageRules
{
    public static IReadOnlyList<IDamageSourceRule> CreateDefault();
}

public sealed class DamageService
{
    public DamageService(Board board, IReadOnlyList<IDamageSourceRule> rules, IMatch3Logger logger);

    /// Сброс учёта «источник → препятствие» на новый шаг разрешения.
    public void BeginStep(int step);

    /// Урон от матч-компонента: 1 экземпляр на источник за шаг. Пишет ElementDamaged.
    public void ApplyMatchDamage(
        ChipColor matchColor,
        IReadOnlyList<GridPos> matchCells,
        int sourceId,
        TranscriptWriter writer);

    /// Урон от одной активации бустера по накрытым клеткам. 1 экземпляр на источник за шаг.
    public void ApplyBoosterDamage(
        IReadOnlyList<GridPos> hitCells,
        int sourceId,
        TranscriptWriter writer);
}

public sealed class ClearService
{
    public ClearService(Board board, IGoalTracker goals, IMatch3Logger logger);

    /// Стадия CLEAR (§5.1, шаг 6):
    /// 1) удаляет фишки/бустеры из chipCells (ChipDestroyed + зачёт CreditChip);
    /// 2) удаляет препятствия с hp = 0 (ElementDestroyed + CreditElement), освобождая клетку;
    /// 3) раскрывает вложенный элемент в той же клетке с ImmuneUntilStep = step
    ///    (ElementRevealed) — иначе одна бомба вскрывает всю цепочку за шаг (§7.1);
    /// 4) пишет GoalProgress на каждую дельту.
    /// deltas — буфер вызывающего, ДОПОЛНЯЕТСЯ.
    public void Clear(
        int step,
        CellBuffer chipCells,
        TranscriptWriter writer,
        PooledList<GoalDelta> deltas);
}
```

Что легко нарушить:

- **Overkill отбрасывается**: перебить 2-hp ящик за шаг на 3 урона «в запас» нельзя.
- Блокер урона **не получает и эффект бустера не останавливает** (`D11`, `E08`).
- Цветной ящик разрушается площадным уроном бустера — это не баг (Q4).
- `AdjacentMatchOfColor` берёт цвет из `ElementInstance.CurrentColor` (общее для `c1`–`c6` и `cx`).
- Клетка уничтоженного препятствия проходима для падения **в том же шаге**.
- Раскрытый вложенный элемент в этом шаге урона не получает, на следующем — получает.

---

## T07 — наведение (`Match3.Boosters`)

```csharp
namespace Match3.Boosters;

/// Одно детерминированное правило на самолётик и на ВСЕ комбинации с радужным шаром (D09, A07).
/// Дубликат этой логики в проекте = находка на ревью.
public interface ITargetingService
{
    /// §6.1: (1) первая незакрытая цель в порядке конфига → (2) среди обслуживающих её клеток
    /// та, чьё уничтожение даёт больше единиц цели (препятствие с 1 hp приоритетнее, чем с 2)
    /// → (3) ничья: мин. y, затем мин. x → (4) нет достижимых незакрытых целей: клетка из RNG
    /// среди непустых клеток с фишками (RandomConsumer.TargetingTieBreak).
    /// GridPos.Invalid, если на поле вообще нет подходящей клетки.
    GridPos PickGoalTarget();

    /// count различных клеток по тому же приоритету, для комбинаций (цели №1, №2, №3).
    /// Дополняет result, не очищая. Возвращает число добавленных.
    int PickGoalTargets(int count, CellBuffer result);

    /// §6.1: (1) цвет первой незакрытой CollectColor → (2) цвет с наибольшим числом фишек
    /// на поле → (3) ничья: минимальный индекс цвета. ChipColor.None, если фишек нет.
    ChipColor PickNeededColor();
}

public sealed class TargetingService : ITargetingService
{
    public TargetingService(IBoardReader board, IGoalTracker goals, IRandom random);
}
```

---

## T08 — бустеры и матрица комбинаций (`Match3.Boosters`)

```csharp
namespace Match3.Boosters;

public readonly struct BoosterActivation
{
    public readonly GridPos Cell;
    public readonly BoosterType Booster;
    /// Цвет для радужного шара: от свапнутой фишки, иначе ChipColor.None → «самый нужный».
    public readonly ChipColor ColorHint;
    public BoosterActivation(GridPos cell, BoosterType booster, ChipColor colorHint);
}

public interface IBoosterEffect
{
    BoosterType Type { get; }

    /// Клетки, задетые срабатыванием. Дополняет hitCells, не очищая.
    /// transforms — клетки, где фишка ПРЕВРАЩАЕТСЯ в бустер (масс-превращения §6.3);
    /// для одиночных бустеров остаётся пустым.
    void Resolve(in BoosterActivation activation, IBoardReader board, CellBuffer hitCells);
}

public sealed class BoosterCatalog
{
    public BoosterCatalog(IReadOnlyList<IBoosterEffect> effects);
    public IBoosterEffect Get(BoosterType type);
}

/// Одна подактивация плана комбинации.
public readonly struct ComboStep
{
    public readonly GridPos Cell;
    public readonly BoosterType Booster;

    /// Задержка перед этой подактивацией, с. Для масс-превращений 0.06–0.08 (§6.3):
    /// одновременная детонация читается как одна вспышка и обесценивает награду.
    public readonly float Delay;

    /// true — в клетке фишка сначала ПРЕВРАЩАЕТСЯ в Booster (ChipTransformed), потом стреляет.
    public readonly bool TransformFirst;
}

/// Упорядоченный план: стадия ACTIVATE (T11) просто исполняет его по порядку.
public sealed class ComboPlan
{
    public GridPos Epicentre { get; }
    public BoosterType A { get; }
    public BoosterType B { get; }
    public IReadOnlyList<ComboStep> Steps { get; }

    /// Клетки, уничтожаемые планом напрямую (без подактивации) — «остальные просто
    /// уничтожаются» при капе 8 самолётиков, и всё поле в «шар+шар».
    public IReadOnlyList<GridPos> DirectCells { get; }

    /// «шар+шар»: 1 урон каждому живому препятствию на поле (§6.3).
    public bool DamagesEveryObstacle { get; }
}

public sealed class ComboResolver
{
    public ComboResolver(IBoardReader board, ITargetingService targeting);

    /// Все 10 клеток матрицы §6.3. Эпицентр — ЦЕЛЕВАЯ клетка свапа (где лежал второй бустер).
    /// Матрица симметрична: (A,B) и (B,A) дают один план.
    public ComboPlan Resolve(GridPos epicentre, BoosterType a, GridPos other, BoosterType b);
}

/// Матрица §6.3 как данные, для тестов и для ComboResolver.
public static class ComboMatrix
{
    public static bool IsCovered(BoosterType a, BoosterType b);
}
```

Эффекты (§6.2):

| Бустер | Клетки |
|---|---|
| Ракета `RocketH` / `RocketV` | вся строка / весь столбец, включая свою клетку, **насквозь** (`D11`) |
| Бомба | квадрат 5×5 с центром в своей клетке, обрезается краем |
| Радужный шар | все фишки цвета `ColorHint`; при `None` — «самый нужный цвет». Fallback, если фишек нет: своя клетка + 4 ортогональных соседа (`E06`/`E07`) |
| Самолётик | целевая клетка от `ITargetingService` + 4 ортогональных соседа |

Матрица комбинаций (все 10 клеток, эпицентр — целевая клетка свапа):

| | Ракета | Бомба | Шар | Самолётик |
|---|---|---|---|---|
| **Ракета** | крест: полная строка + полный столбец через эпицентр | толстый крест: строки `y−1…y+1` и столбцы `x−1…x+1` целиком | все фишки нужного цвета → ракеты; ориентация чередуется по обходу `(y↑, x↑)`: чётный индекс — горизонтальная; шаг 0.06 с | 2 самолётика к целям №1/№2; в клетке удара появляется ракета и стреляет немедленно (первая горизонтальная, вторая вертикальная) |
| **Бомба** | — | один взрыв 7×7 с центром в эпицентре | все фишки нужного цвета → бомбы, шаг 0.08 с | 2 самолётика к целям №1/№2, каждый доставляет 5×5 |
| **Шар** | — | — | всё поле: все фишки, каждому живому препятствию 1 урон | фишки нужного цвета → самолётики, **кап 8** (`ResolveCaps.MaxAirplanesInCombo`, отбор по `(y↑, x↑)`, остальные просто уничтожаются), шаг 0.08 с |
| **Самолётик** | — | — | — | 3 самолётика к целям №1/№2/№3 |

---

## T04 — формат уровня (`Match3.Levels`)

```csharp
namespace Match3.Levels;

public readonly struct NestedContent
{
    public readonly int X, Y;
    public readonly string Token;   // внешний → внутренний по порядку в списке
}

public sealed class LevelData
{
    public int Id { get; }
    public int Width { get; }
    public int Height { get; }
    public int ColorCount { get; }              // [4, 6]
    public int MoveLimit { get; }
    public DifficultyTier Tier { get; }
    public IReadOnlyList<GoalDefinition> Goals { get; }        // порядок = отображение И приоритет
    public IReadOnlyList<string> LayoutRows { get; }           // первая строка = y = height − 1
    public IReadOnlyList<NestedContent> Contents { get; }
    public IReadOnlyList<int> Spawners { get; }                // пусто = дефолт §3.3
    public IReadOnlyList<ColorWeight> SpawnWeights { get; }     // пусто = равномерно
    public float HintDelaySeconds { get; }                      // 0 = подсказка выключена
}

/// Сетка токенов после единственного флипа строк (D01). TokenAt(x, y) — уже в координатах поля.
public sealed class TokenGrid
{
    public int Width { get; }
    public int Height { get; }
    public string TokenAt(int x, int y);
}

public static class LayoutParser
{
    /// Токен = 2 символа, разделитель — пробел. Первая строка = y = height − 1, флип
    /// делается РОВНО ОДИН РАЗ здесь. Внятные ошибки: неизвестный токен, длина токена,
    /// число строк, ширина строки.
    public static TokenGrid Parse(IReadOnlyList<string> rows, int width, int height, ElementCatalog catalog);
}

public enum ValidationSeverity : byte { Warning = 0, Error = 1 }

public readonly struct ValidationIssue
{
    public readonly ValidationSeverity Severity;
    public readonly string Message;
}

public sealed class ValidationReport
{
    public IReadOnlyList<ValidationIssue> Issues { get; }
    public bool HasErrors { get; }
    public string Describe();
}

public sealed class LevelValidator
{
    public LevelValidator(ElementCatalog catalog);

    /// §3.4: достижимость каждой играемой клетки от спавнера (непроходимы только `__`
    /// и блокер), нет готовых матчей, есть легальный ход, цели достижимы, вложенность ≤ 3,
    /// colorCount ∈ [4,6], соответствие размеров, неизвестные токены.
    /// Сплошной блок стен шире 2 → предупреждение (§10.3).
    public ValidationReport Validate(LevelData level, IRandom random);
}

public sealed class FlowReachabilityAnalyzer
{
    public FlowReachabilityAnalyzer(ElementCatalog catalog);
    public bool IsReachableFromSpawner(TokenGrid grid, IReadOnlyList<int> spawners, int x, int y);
}

public sealed class BoardBuilder
{
    public BoardBuilder(ElementCatalog catalog, IMatch3Logger logger);

    /// LevelData + IRandom → Board. E20: заполнение y↑, x↑; при замыкании линии из 3 цвет
    /// перевыбирается (до colorCount − 1 попыток, затем любой не образующий линию); затем
    /// проверка легального хода, до ResolveCaps-эквивалента 50 регенераций, затем ремонтный
    /// проход свапами. Порядок потребления RNG — §13.
    public Board Build(LevelData level, IRandom random);
}

public static class MoveLimitCalculator
{
    /// §9.3: work = max(цветовые: target × cost) + Σ(остальные: target × cost);
    /// moves = ceil(work / thr × multiplier). Инструмент валидации, не рантайм-логика.
    public static int Compute(LevelData level, float throughput);

    public static float CostPerUnit(in GoalDefinition goal, int colorCount, ElementCatalog catalog);
    public static float MultiplierFor(DifficultyTier tier);
}
```

Хелпер для тестов (в `Match3.Tests.EditMode`, не в модуле):
`TestLevel.From(string layout, ...)` → `LevelData`.

---

## T11–T13 — цикл разрешения, ход, подсказка

Эти задачи собирает автор контракта; их публичная поверхность — `TurnRule`, `ResolveLoopService`,
`HintService` по `docs/architecture.md` §7. Задачам T04–T10 они не нужны: обратных вызовов из
T11+ в T04–T10 нет, только прямые.

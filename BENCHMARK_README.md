# AI Benchmark и расширенная игра: как запускать

## Что изменилось

- Игра и симуляция AI используют один каталог карт (`CardCatalog`): 14 типов,
  колода 30 карт.
- HP героев — 30, максимальная мана — 10.
- Добавлены ключевые слова: `Taunt`, `Charge`, `Divine Shield`, `Lifesteal`.
- Добавлена усталость: при пустой колоде герой получает 1, 2, 3... урона.
- Стартовые руки: первый игрок 3 карты + добор = 4, второй 4 карты + добор = 5.
- Второй игрок получает +1 ману на свой первый ход (компенсация первого хода).
- Колода перемешивается один раз, карта берётся сверху.
- Бенчмарк собирает статистику по первому/второму ходу и зеркальным парам.
- `SimGameState.Clone()` больше не использует JSON — это главное ускорение.

## Сначала сгенерируйте prefab'ы карт

В проекте уже есть код новых карт, но prefab'ы и списки колод создаёт
Editor-генератор.

1. Сделайте backup/commit `Assets/Scenes/SampleScene.unity` и
   `Assets/Prefabs/Cards`.
2. Откройте Unity 2022.3.34f1.
3. Дождитесь компиляции.
4. Запустите `Tools -> Cards -> Generate Expanded Set`.
5. Проверьте, что в `Assets/Prefabs/Cards/Generated` появились prefab'ы,
   а в `SampleScene` колоды содержат 30 карт.
6. Запустите сцену и проверьте стартовые руки, ману, HP и новые карты.

Генератор можно запустить в batchmode:

```bat
"C:\Program Files\Unity\Hub\Editor\2022.3.34f1\Editor\Unity.exe" -batchmode -nographics -quit -projectPath "C:\path\to\CardGameUnity" -executeMethod CardPrefabGenerator.GenerateFromCommandLine -logFile "C:\path\to\card_generator.log"
```

Генератор пересоздаёт папку `Generated` и пересобирает оба списка `Deck.deck`.
Старые prefab'ы Goblin/Thief/Knight/Mage остаются в проекте, но колоды
используют сгенерированные карты. Новые карты пока используют шаблонный арт
Goblin; при необходимости замените спрайты в сгенерированных prefab'ах.

## Запуск бенчмарка из Unity

- `Tools -> AI Benchmark -> Run Gauntlet` — основной быстрый режим;
- `Tools -> AI Benchmark -> Run Round Robin` — полный круг, дорого.

Результаты появятся в `Game/CardGameUnity/BenchmarkResults`.

## Запуск бенчмарка в batchmode

```bat
"C:\Program Files\Unity\Hub\Editor\2022.3.34f1\Editor\Unity.exe" -batchmode -nographics -quit -projectPath "C:\path\to\CardGameUnity" -executeMethod AIBenchmarkRunner.RunFromCommandLine -benchMode gauntlet -benchMatches 10 -benchSeed 12345 -logFile "C:\path\to\unity_benchmark.log"
```

Параметры:

- `-benchMode gauntlet|roundrobin`;
- `-benchMatches N` — партий на пару (по умолчанию 10);
- `-benchSeed N` — seed (по умолчанию 12345).

Для round-robin начните с `-benchMatches 4`: 19 профилей дают 171 пару.

## Настройки в инспекторе

| Поле | По умолчанию | Смысл |
|---|---:|---|
| `mode` | Gauntlet | Режим построения пар |
| `gauntletOpponents` | Rule, AB-D2 | Эталонные противники |
| `matchesPerPair` | 10 | Партий на пару |
| `maxActionsPerMatch` | 500 | Предохранитель от зависания |
| `maxMatchSeconds` | 60 | Лимит на одну партию |
| `maxTotalMinutes` | 60 | Лимит на весь прогон |
| `randomSeed` | 12345 | Seed колод и MCTS |
| `alternateFirstPlayer` | true | Чередовать первого игрока |
| `useTiebreak` | true | Тай-брейк при исчерпании лимита действий |
| `heroHp` | 30 | HP героев |
| `maxMana` | 10 | Максимальная мана |
| `boardSize` | 3 | Слотов на столе |
| `firstStartingHand` | 4 | Стартовая рука первого |
| `secondStartingHand` | 4 | Стартовая рука второго |
| `secondPlayerFirstTurnBonusMana` | 1 | Бонус маны второму на первый ход |
| `identicalDecks` | true | Одинаковый порядок карт у обоих |
| `outputFolderName` | BenchmarkResults | Куда писать результаты |

Если партия не завершилась за `maxActionsPerMatch`, применяется тай-брейк:
здоровье героев, затем сила стола. Такие партии помечаются флагом `tiebreak`.

## Выходные файлы

- `benchmark_matches.csv` — по одной строке на партию;
- `benchmark_pairs.csv` — сводка по парам, включая победы первого/второго;
- `benchmark_summary.csv` — итоговая таблица, включая winrate как первый/второй;
- `benchmark_summary.md` — готовая Markdown-таблица для отчёта;
- `benchmark_log.txt` — подробный лог.

## Ожидаемое время

На .NET-прогоне 19 профилей × 2 эталона × 10 партий заняли около 1.2 минуты.
В Unity/Mono будет медленнее, но за счёт быстрого `Clone()`, 30 HP и gauntlet
речь идёт о минутах, а не часах. Если долго:

1. уменьшите `matchesPerPair` до 4–6;
2. уменьшите `maxTotalMinutes`;
3. не включайте round-robin для всех 19 профилей.

## Профили

| Короткое имя | Что включает |
|---|---|
| Rule | Жадный rule-based агент |
| MM-D2 | Минимакс без α-β, глубина 2 |
| AB-D2 | α-β + сортировка, глубина 2 |
| AB-D3 | α-β + сортировка, глубина 3 |
| AB-NO-D3 | α-β без сортировки, глубина 3 |
| AB-ID | Итеративное углубление, до глубины 4, 100 мс |
| AB-TT-D3 | α-β + таблица транспозиций, глубина 3 |
| AB-Q-D2/D3/D4 | α-β + quiescence, глубина 2/3/4 |
| AB-Q-TT-D3/D4 | quiescence + TT, глубина 3/4 |
| AB-Q-TT-ID | quiescence + TT + ID, 150 мс, до глубины 5 |
| AB-Q-PVS-D3 | quiescence + PVS + TT, глубина 3 |
| AB-ALL | quiescence + TT + PVS + ID, 200 мс, до глубины 5 |
| AB-PVS-D3 | PVS + TT + сортировка, глубина 3 |
| EXP-D2 | Expectimax с chance-узлами добора |
| PIMC-D2 | PIMC, 6 детерминизаций, глубина 2 |
| MCTS-300 | MCTS, 300 итераций, rollout 6 |

## Как читать статистику первого хода

- `first_wins` / `first_matches` — победы и матчи, где вариант ходил первым;
- `second_wins` / `second_matches` — то же для второго хода;
- `seat_balanced` — среднее между winrate первого и второго хода;
- `Первый игрок выиграл ...` — общая доля побед первого игрока;
- `Один и тот же агент выиграл обе партии` — зеркальные пары, где победитель
  не зависел от места;
- `Победители разные` — зеркальные пары, где место/колода решили исход.

Если у варианта высокая разница между первым и вторым ходом, его результат
менее надежен, даже если общий winrate высокий.

## Калибровка компенсации второго игрока

По умолчанию второй игрок получает +1 ману на первый ход. Если после прогона
первый игрок всё ещё выигрывает слишком часто, увеличьте
`secondPlayerFirstTurnBonusMana` до 2 и повторите бенчмарк; если второй начал
выигрывать чаще, верните 0. Ориентир — 50–55\% побед первого игрока. В
реальной игре это же значение задаётся в `Order` и в генераторе карт.

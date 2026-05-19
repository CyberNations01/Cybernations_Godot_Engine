# TeamGoal Conditions

Source: `ServerForTest/data/goal.json`

Server evaluation path:

- `DataLoader::parseJson<Goal>()` parses `victory_condition`.
- `GameState::isActiveGoalMet()` requires every parsed condition to pass.

## Rule Notes

- `EQ`: equal to
- `GE`: greater than or equal to
- `GT`: greater than
- `LE`: less than or equal to
- `LT`: less than
- `NE`: not equal to
- Stack conditions count the effective stack type on board tiles.
- Resource conditions read from game parameters.
- In the notes column, all listed conditions must be met at the same time.
- Position filters:
  - no position: any board position
  - `inner`: position `0`
  - `middle`: positions `1` through `6`
  - `outer`: positions `7` through `10`

## Resource Labels

- `HR`: Human Relation
- `Co`: Cohesion
- `Env`: Environment
- `Tech`: Technology
- `Cy`: Cybernation Level
- `Wild`: Wild stack tiles
- `Waste`: Waste stack tiles
- `DevA`: Development A stack tiles
- `DevB`: Development B stack tiles

## Goals

| ID | TeamGoal | Reverse Goal | Conditions | 备注 |
| --- | --- | --- | --- | --- |
| 0 | Restore and Rewild | 1 | `Wild EQ 11`<br>`HR GE 11` | 棋盘上 Wild 地块数量等于 11，且 Human Relation 至少为 11。 |
| 1 | Dominate the Land | 0 | `Wild EQ 0`<br>`Co GE 10` | 棋盘上没有 Wild 地块，且 Cohesion 至少为 10。 |
| 2 | Prepare for the Worst | 3 | `Wild GE 2`<br>`DevA GE 3`<br>`Cy GE 7` | 棋盘上 Wild 地块至少有 2 个，DevA 地块至少有 3 个，且 Cybernation Level 至少为 7。 |
| 3 | Each to their Own | 2 | `DevB GE 6`<br>`DevA EQ 0`<br>`Cy EQ 0` | 棋盘上 DevB 地块至少有 6 个，没有 DevA 地块，且 Cybernation Level 等于 0。 |
| 4 | Reconnect | 5 | `Waste EQ 0`<br>`HR GE 12`<br>`Tech GE 12`<br>`Env GE 12` | 棋盘上没有 Waste 地块，且 Human Relation、Technology、Environment 都至少为 12。 |
| 5 | Ransack | 4 | `Waste EQ 11`<br>`HR GE 5`<br>`Tech GE 5`<br>`Env GE 5` | 棋盘上 Waste 地块数量等于 11，且 Human Relation、Technology、Environment 都至少为 5。 |
| 6 | Equity | 7 | `DevA EQ 4` at `outer`<br>`Wild GE 3` at `middle`<br>`Cy GE 4` | 外圈 DevA 地块数量等于 4，中圈 Wild 地块至少有 3 个，且 Cybernation Level 至少为 4。 |
| 7 | Inequity | 6 | `DevA EQ 1` at `inner`<br>`Wild GE 3` at `outer`<br>`Cy GE 4` | 内圈 DevA 地块数量等于 1，外圈 Wild 地块至少有 3 个，且 Cybernation Level 至少为 4。 |
| 8 | Tomorrow through Tech | 9 | `DevB EQ 1` at `inner`<br>`Co GE 15` | 内圈 DevB 地块数量等于 1，且 Cohesion 至少为 15。 |
| 9 | Back to Nature | 8 | `DevB EQ 0`<br>`Wild GE 6`<br>`Co GE 20` | 棋盘上没有 DevB 地块，Wild 地块至少有 6 个，且 Cohesion 至少为 20。 |

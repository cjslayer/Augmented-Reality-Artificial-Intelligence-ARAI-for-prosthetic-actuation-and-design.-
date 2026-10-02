# Run 014 smoke (results/014_smoke, slot layout)

Episode logs: 8 files, 3565 episodes.

TensorBoard: last summary step 500000, 122 scalar tags.

## Composition shares (episode logs)

| composition | slots | episodes | share [95 %] | design | inside? |
|---|---|---|---|---|---|
| 3 | 1110000 | 373 | 0.105 [0.095, 0.115] | 0.100 | yes |
| 4 | 1111000 | 515 | 0.144 [0.133, 0.156] | 0.150 | yes |
| 5 | 1111100 | 1577 | 0.442 [0.426, 0.459] | 0.450 | yes |
| 6a | 1111110 | 266 | 0.075 [0.066, 0.084] | 0.075 | yes |
| 6b | 1111101 | 295 | 0.083 [0.074, 0.092] | 0.075 | yes |
| 7 | 1111111 | 539 | 0.151 [0.140, 0.163] | 0.150 | yes |

All six shares consistent with the design: **YES**

## Per-digit-count StatsRecorder channels (TensorBoard)

| channel | summaries | last value |
|---|---|---|
| Success/Digits3 | 50 | 0.0000 |
| Success/Digits4 | 50 | 0.0000 |
| Success/Digits5 | 50 | 0.0000 |
| Success/Digits6 | 50 | 0.0952 |
| Success/Digits7 | 49 | 0.0000 |
| Gate/Digits3 | 50 | 0.8000 |
| Gate/Digits4 | 50 | 1.0000 |
| Gate/Digits5 | 50 | 1.0000 |
| Gate/Digits6 | 50 | 1.0000 |
| Gate/Digits7 | 49 | 0.9333 |
| Lift/Digits3 | 50 | 0.2000 |
| Lift/Digits4 | 50 | 0.3182 |
| Lift/Digits5 | 50 | 0.2553 |
| Lift/Digits6 | 50 | 0.2381 |
| Lift/Digits7 | 49 | 0.1333 |
| Success/Digits6a | 49 | 0.2222 |
| Success/Digits6b | 49 | 0.0000 |
| Comp/Share3 | 50 | 0.1250 |
| Comp/Share4 | 50 | 0.1833 |
| Comp/Share5 | 50 | 0.3917 |
| Comp/Share6a | 50 | 0.0750 |
| Comp/Share6b | 50 | 0.1000 |
| Comp/Share7 | 50 | 0.1250 |
| Effort/Digits3 | 50 | -0.0578 |
| Effort/Digits4 | 50 | -0.0370 |
| Effort/Digits5 | 50 | -0.0234 |
| Effort/Digits6a | 49 | -0.0189 |
| Effort/Digits6b | 49 | -0.0327 |
| Effort/Digits7 | 49 | -0.0425 |
| Spawn/OverlapRejectRate3 | 50 | 0.2833 |
| Spawn/OverlapRejectRate4 | 50 | 0.2576 |
| Spawn/OverlapRejectRate5 | 50 | 0.1598 |
| Spawn/OverlapRejectRate6a | 49 | 0.3019 |
| Spawn/OverlapRejectRate6b | 49 | 0.1389 |
| Spawn/OverlapRejectRate7 | 49 | 0.3089 |

All channels populated: **YES**

## NaN / inf in any scalar: **none**

## Effort return on 3-digit hands: 373 episodes, all finite: **YES**; mean -0.08789, min -0.15651, max -0.00555

| composition | episodes | mean effort return | mean squared action per present joint and step |
|---|---|---|---|
| 3 | 236 | -0.05391 | 0.1148 |
| 4 | 405 | -0.04096 | 0.1163 |
| 5 | 1449 | -0.03315 | 0.1167 |
| 6a | 246 | -0.03697 | 0.1161 |
| 6b | 264 | -0.03529 | 0.1158 |
| 7 | 510 | -0.03853 | 0.1156 |

## Spawn rejection by the open-hand overlap filter (rejected / reach-accepted candidates)

| composition | candidates | rejected | rate | rate − 5-digit | fallback episodes |
|---|---|---|---|---|---|
| 3 | 628 | 255 | 0.4061 | +0.0216 | 0 |
| 4 | 842 | 327 | 0.3884 | +0.0039 | 0 |
| 5 | 2562 | 985 | 0.3845 | +0.0000 | 0 |
| 6a | 436 | 170 | 0.3899 | +0.0054 | 0 |
| 6b | 480 | 185 | 0.3854 | +0.0010 | 0 |
| 7 | 940 | 401 | 0.4266 | +0.0421 **> 3 pp** | 0 |

Compositions differing from the 5-digit rate by more than 3 percentage points: **7**

## One episode per composition with the gate, the lift and the hold firing (episode logs)

| composition | episode | success | end | steps | transition step | steps to lift | hold steps / needed (K) | contacts | thumb | palm |
|---|---|---|---|---|---|---|---|---|---|---|
| 3 | 156 | 1 | success | 4000 | 3839 | 3951 | 50 / 50 (5) | 7 | 1 | 1 |
| 4 | 73 | 1 | success | 1921 | 1603 | 1853 | 50 / 50 (5) | 6 | 1 | 0 |
| 5 | 50 | 1 | success | 1155 | 1092 | 1106 | 50 / 50 (5) | 7 | 1 | 1 |
| 6a | 342 | 1 | success | 254 | 91 | 205 | 50 / 50 (5) | 9 | 1 | 1 |
| 6b | 362 | 1 | success | 675 | 402 | 626 | 50 / 50 (5) | 6 | 1 | 0 |
| 7 | 70 | 1 | success | 1963 | 1790 | 1914 | 50 / 50 (5) | 7 | 1 | 1 |

Gate, lift and hold fire on every composition: **YES**

## Rates by composition over the smoke (episode logs)

| composition | episodes | reach failure (MaxStep) | gate | lift | hold entered | success |
|---|---|---|---|---|---|---|
| 3 | 373 | 0.367 | 0.633 | 0.150 | 0.110 | 0.051 |
| 4 | 515 | 0.214 | 0.786 | 0.217 | 0.190 | 0.019 |
| 5 | 1577 | 0.081 | 0.919 | 0.233 | 0.199 | 0.019 |
| 6a | 266 | 0.075 | 0.925 | 0.211 | 0.184 | 0.019 |
| 6b | 295 | 0.105 | 0.895 | 0.200 | 0.166 | 0.003 |
| 7 | 539 | 0.054 | 0.946 | 0.134 | 0.111 | 0.015 |

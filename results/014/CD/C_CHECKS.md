
## C+0.5M check: trainer step 24510000 (phase C since 24000187, +0.51M; table window last 500k; 438 steps/s over this log)

Pooled lying success per 500k window since C began: 24.50M 0.106 (n 7282)

Pooled lying lift per 1M window since C began (reference B last 1M: 0.435): none complete

Switch criterion (trailing 500k lying success ≥ 0.40, or step ≥ 32M): trailing 500k = 0.107 (n 7278) → **not met**
Stop rule (lying lift falls in two consecutive 1M windows AND < 0.35): **not met**

**Lying** (last 500k)

| composition | episodes | gate | lift | hold | success | end drop | end liftBudget | end maxStep | end success |
|---|---|---|---|---|---|---|---|---|---|
| 3 | 722 | 0.802 | 0.325 | 0.303 | 0.075 | 0.367 | 0.350 | 0.202 | 0.075 |
| 4 | 1072 | 0.979 | 0.435 | 0.410 | 0.095 | 0.431 | 0.445 | 0.023 | 0.095 |
| 5 | 3283 | 0.993 | 0.505 | 0.481 | 0.125 | 0.475 | 0.386 | 0.008 | 0.125 |
| 6a | 553 | 0.982 | 0.512 | 0.486 | 0.110 | 0.476 | 0.385 | 0.020 | 0.110 |
| 6b | 562 | 0.998 | 0.479 | 0.452 | 0.109 | 0.464 | 0.422 | 0.002 | 0.109 |
| 7 | 1086 | 0.993 | 0.436 | 0.403 | 0.084 | 0.449 | 0.448 | 0.008 | 0.084 |
| all | 7278 | 0.971 | 0.465 | 0.439 | 0.107 | 0.453 | 0.403 | 0.030 | 0.107 |

## C+1M report: trainer step 25000000 (phase C since 24000187, +1.00M; table window last 1000k; 439 steps/s over this log)

Pooled lying success per 500k window since C began: 24.50M 0.106 (n 7248)

Pooled lying lift per 1M window since C began (reference B last 1M: 0.435): none complete

Switch criterion (trailing 500k lying success ≥ 0.40, or step ≥ 32M): trailing 500k = 0.160 (n 7263) → **not met**
Stop rule (lying lift falls in two consecutive 1M windows AND < 0.35): **not met**

**Lying** (last 1000k)

| composition | episodes | gate | lift | hold | success | end drop | end liftBudget | end maxStep | end success |
|---|---|---|---|---|---|---|---|---|---|
| 3 | 1424 | 0.821 | 0.336 | 0.312 | 0.088 | 0.365 | 0.360 | 0.183 | 0.088 |
| 4 | 2121 | 0.979 | 0.436 | 0.415 | 0.120 | 0.417 | 0.433 | 0.024 | 0.120 |
| 5 | 6574 | 0.993 | 0.510 | 0.486 | 0.155 | 0.449 | 0.383 | 0.007 | 0.155 |
| 6a | 1109 | 0.982 | 0.495 | 0.475 | 0.144 | 0.428 | 0.401 | 0.019 | 0.144 |
| 6b | 1116 | 0.996 | 0.473 | 0.451 | 0.113 | 0.457 | 0.424 | 0.004 | 0.113 |
| 7 | 2167 | 0.992 | 0.441 | 0.413 | 0.110 | 0.429 | 0.444 | 0.009 | 0.110 |
| all | 14511 | 0.973 | 0.468 | 0.444 | 0.133 | 0.432 | 0.402 | 0.028 | 0.133 |

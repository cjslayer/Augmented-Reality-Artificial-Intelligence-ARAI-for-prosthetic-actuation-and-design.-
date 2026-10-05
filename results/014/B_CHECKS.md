# Run 014 attempt 2, phase B checks (training stats only; sampled head)

Rule (CJ 2026-10-05): stop and report if pooled upright lift falls in two consecutive 500k windows AND is below 0.5 (switch value 0.771). Upright success and lying have no stop criterion. Checks every 500k at 2.75 / 3.25 / 3.75 / 4.25M, then every 1M. Upright = canonical pose, lying = randomStable.

## Check at 2.75M (trainer step 2790000; last 500k, 2290000 to 2790000; 432 steps/s)

Pooled upright lift per 500k window since B began (2.25M): 2.75M 0.746 (n 1943)

Stop rule (two consecutive falls AND below 0.5): **not met**

**Upright**

| composition | episodes | gate | lift | hold | success | end drop | end liftBudget | end maxStep | end success |
|---|---|---|---|---|---|---|---|---|---|
| 3 | 216 | 0.870 | 0.634 | 0.634 | 0.417 | 0.417 | 0.037 | 0.130 | 0.417 |
| 4 | 313 | 0.997 | 0.773 | 0.760 | 0.559 | 0.425 | 0.013 | 0.003 | 0.559 |
| 5 | 863 | 1.000 | 0.796 | 0.787 | 0.598 | 0.388 | 0.014 | 0.000 | 0.598 |
| 6a | 162 | 0.994 | 0.667 | 0.667 | 0.512 | 0.469 | 0.012 | 0.006 | 0.512 |
| 6b | 151 | 0.993 | 0.748 | 0.742 | 0.497 | 0.497 | 0.000 | 0.007 | 0.497 |
| 7 | 306 | 1.000 | 0.680 | 0.657 | 0.428 | 0.549 | 0.023 | 0.000 | 0.428 |
| all | 2011 | 0.985 | 0.743 | 0.733 | 0.532 | 0.436 | 0.016 | 0.015 | 0.532 |

**Lying**

| composition | episodes | gate | lift | hold | success | end drop | end liftBudget | end maxStep | end success |
|---|---|---|---|---|---|---|---|---|---|
| 3 | 194 | 0.289 | 0.067 | 0.067 | 0.036 | 0.077 | 0.160 | 0.727 | 0.036 |
| 4 | 292 | 0.682 | 0.106 | 0.103 | 0.072 | 0.182 | 0.421 | 0.325 | 0.072 |
| 5 | 883 | 0.900 | 0.104 | 0.100 | 0.057 | 0.277 | 0.561 | 0.105 | 0.057 |
| 6a | 154 | 0.890 | 0.058 | 0.058 | 0.032 | 0.286 | 0.565 | 0.117 | 0.032 |
| 6b | 143 | 0.902 | 0.091 | 0.077 | 0.021 | 0.455 | 0.420 | 0.105 | 0.021 |
| 7 | 298 | 0.940 | 0.091 | 0.081 | 0.034 | 0.416 | 0.490 | 0.060 | 0.034 |
| all | 1964 | 0.813 | 0.094 | 0.089 | 0.049 | 0.278 | 0.480 | 0.193 | 0.049 |

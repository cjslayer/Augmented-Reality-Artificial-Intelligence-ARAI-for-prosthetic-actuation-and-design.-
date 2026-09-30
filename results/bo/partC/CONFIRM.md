# Part C confirmation (held-out shapes 65-80, seeds 11501-11800, n = 300 per presentation)

| theta | source | canonical | random stable | both |
|---|---|---|---|---|
| bo1 | bo #68 (campaign J 0.930) | 0.970 [0.944, 0.984] (n=300) | 0.773 [0.723, 0.817] (n=300) | 0.872 [0.843, 0.896] (n=600) |
| bo2 | bo #66 (campaign J 0.922) | 0.967 [0.940, 0.982] (n=300) | 0.800 [0.751, 0.841] (n=300) | 0.883 [0.855, 0.907] (n=600) |
| bo3 | bo #69 (campaign J 0.914) | 0.987 [0.966, 0.995] (n=300) | 0.777 [0.726, 0.820] (n=300) | 0.882 [0.853, 0.905] (n=600) |
| random1 | random #4 (campaign J 0.898) | 0.943 [0.911, 0.964] (n=300) | 0.830 [0.783, 0.868] (n=300) | 0.887 [0.859, 0.910] (n=600) |
| random2 | random #81 (campaign J 0.883) | 0.967 [0.940, 0.982] (n=300) | 0.787 [0.737, 0.829] (n=300) | 0.877 [0.848, 0.901] (n=600) |
| random3 | random #1 (campaign J 0.875) | 0.990 [0.971, 0.997] (n=300) | 0.770 [0.719, 0.814] (n=300) | 0.880 [0.852, 0.904] (n=600) |
| reference | spec nominal hand | 0.970 [0.944, 0.984] (n=300) | 0.740 [0.688, 0.786] (n=300) | 0.855 [0.825, 0.881] (n=600) |
| bo1_thumbBaseOff | bo1 with mask bit 12 (thumb base) off | 0.003 [0.001, 0.019] (n=300) | 0.243 [0.198, 0.295] (n=300) | 0.123 [0.099, 0.152] (n=600) |

Paired differences (same seed and shape rows; 95 % CI from the paired SD):

| comparison | canonical | random stable | both |
|---|---|---|---|
| BO-best (bo1) − reference | +0.000 [-0.024, +0.024] | +0.033 [-0.023, +0.090] | +0.017 [-0.014, +0.048] |
| BO-best (bo1) − random-best (random1, by campaign J) | +0.027 [-0.001, +0.054] | -0.057 [-0.108, -0.005] | -0.015 [-0.044, +0.014] |
| BO-best (bo1) − best random on the confirmation set (random1) | +0.027 [-0.001, +0.054] | -0.057 [-0.108, -0.005] | -0.015 [-0.044, +0.014] |
| bo1 thumb base OFF − bo1 | -0.967 [-0.987, -0.946] | -0.530 [-0.592, -0.468] | -0.748 [-0.785, -0.711] |
| random1 − reference | -0.027 [-0.053, -0.001] | +0.090 [+0.042, +0.138] | +0.032 [+0.004, +0.059] |

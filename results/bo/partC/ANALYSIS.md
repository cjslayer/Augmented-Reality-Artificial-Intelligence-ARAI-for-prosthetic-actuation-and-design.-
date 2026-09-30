# Part C campaign analysis

## Best-so-far (observed J, 128 episodes per point)

| evaluations | random | BO |
|---|---|---|
| 1 | 0.633 | 0.859 |
| 10 | 0.898 | 0.891 |
| 20 | 0.898 | 0.891 |
| 30 | 0.898 | 0.891 |
| 40 | 0.898 | 0.891 |
| 50 | 0.898 | 0.906 |
| 60 | 0.898 | 0.906 |
| 70 | 0.898 | 0.930 |
| 80 | 0.898 | 0.930 |
| 90 | 0.898 | 0.930 |
| 100 | 0.898 | 0.930 |

random: mean J 0.782, SD 0.062, min 0.633, max 0.898

bo: mean J 0.852, SD 0.053, min 0.656, max 0.930; BO sequential points 31-100 mean 0.877 vs Sobol 1-30 mean 0.794

## GP relevance (gp_hparams_100.json: n 100, signal variance 0.0024, noise variance 0.0020, noise floor 0.002)

Length scales are in standardized-input units (inputs z-scored per dimension); smaller = more relevant.

| rank | dimension | length scale | inverse |
|---|---|---|---|
| 1 | k_wrist | 2.13 | 0.470 |
| 2 | zeta_thumb | 6.14 | 0.163 |
| 3 | k_MCP | 7.90 | 0.127 |
| 4 | inertia_DIP | 8.27 | 0.121 |
| 5 | zeta_PIP | 8.93 | 0.112 |
| 6 | zeta_DIP | 9.55 | 0.105 |
| 7 | len_index | 9.81 | 0.102 |
| 8 | k_PIP | 11.02 | 0.091 |
| 9 | len_middle | 12.22 | 0.082 |
| 10 | k_thumb | 13.12 | 0.076 |

Length-scale history (top-5 dimensions of the last fit):

| file | k_wrist | zeta_thumb | k_MCP | inertia_DIP | zeta_PIP |
|---|---|---|---|---|---|
| gp_hparams_030.json | 1.00 | 1.00 | 1.00 | 1.00 | 1.00 |
| gp_hparams_040.json | 1.09 | 5.20 | 2.47 | 5.22 | 6.77 |
| gp_hparams_050.json | 1.36 | 4.91 | 3.58 | 5.57 | 7.83 |
| gp_hparams_060.json | 1.55 | 5.14 | 4.34 | 6.39 | 8.56 |
| gp_hparams_070.json | 1.76 | 5.24 | 4.87 | 6.56 | 6.95 |
| gp_hparams_080.json | 1.87 | 5.58 | 5.73 | 7.26 | 7.70 |
| gp_hparams_090.json | 1.99 | 5.92 | 6.57 | 7.78 | 8.28 |
| gp_hparams_100.json | 2.13 | 6.14 | 7.90 | 8.27 | 8.93 |

## Best theta per arm vs reference (natural units; k in N m/rad, thumb k = thumb-base group, the thumb-end group takes the same quantile of its range)

| dimension | reference | random best (#4, J 0.898) | bo best (#68, J 0.930) |
|---|---|---|---|
| len_index | 1.000 | 1.020 | 0.831 |
| len_middle | 1.000 | 0.936 | 1.183 |
| len_ring | 1.000 | 1.083 | 1.077 |
| len_pinky | 1.000 | 0.955 | 1.133 |
| len_thumb | 1.000 | 0.946 | 1.088 |
| k_MCP | 1.732 | 0.978 | 1.736 |
| k_PIP | 0.949 | 0.969 | 1.515 |
| k_DIP | 0.316 | 0.943 | 1.000 |
| k_thumb | 3.873 | 5.793 | 15.000 |
| k_wrist | 3.162 | 8.182 | 9.423 |
| zeta_MCP | 0.700 | 0.444 | 0.526 |
| zeta_PIP | 0.700 | 0.920 | 0.307 |
| zeta_DIP | 0.700 | 0.358 | 0.978 |
| zeta_thumb | 0.700 | 0.382 | 0.300 |
| zeta_wrist | 0.700 | 0.462 | 0.921 |
| inertia_MCP | 1.000 | 0.736 | 1.461 |
| inertia_PIP | 1.000 | 1.681 | 1.176 |
| inertia_DIP | 1.000 | 1.342 | 1.949 |
| inertia_thumb | 1.000 | 1.667 | 1.194 |
| inertia_wrist | 1.000 | 1.051 | 1.306 |

## Reference drift (J of the reference theta during the campaign)

| after evaluations | J | canonical | random stable | hash (canonical / random stable) |
|---|---|---|---|---|
| 0 | 0.859 | 0.969 | 0.750 | f8816016 / 1329567a |
| 20 | 0.859 | 0.969 | 0.750 | f8816016 / 1329567a |
| 40 | 0.859 | 0.969 | 0.750 | f8816016 / 1329567a |
| 60 | 0.859 | 0.969 | 0.750 | f8816016 / 1329567a |
| 80 | 0.859 | 0.969 | 0.750 | f8816016 / 1329567a |
| 100 | 0.875 | 1.000 | 0.750 | 012ba17e / 1329567a |
| 120 | 0.891 | 1.000 | 0.781 | 012ba17e / f75fa092 |
| 140 | 0.859 | 0.969 | 0.750 | f8816016 / 1329567a |
| 160 | 0.859 | 0.969 | 0.750 | f8816016 / 1329567a |
| 180 | 0.859 | 0.969 | 0.750 | f8816016 / 1329567a |
| 200 | 0.859 | 0.969 | 0.750 | f8816016 / 1329567a |

max |J - first| = 0.031 (limit 0.10); SD 0.010

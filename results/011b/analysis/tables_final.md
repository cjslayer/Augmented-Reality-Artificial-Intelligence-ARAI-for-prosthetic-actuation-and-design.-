
**1a. Mass tercile (edges 0.386 / 0.762 kg from the mu 1.0 file) x end phase**

| mu | mass | n | success | drop:before-lift | drop:during-pulse-1 | drop:hold-before-pulse-1 | drop:later-after-pulse-1 | drop:later-after-pulse-2 | drop:later-after-pulse-3 | drop:within-0.4s-after-pulse-1 | drop:within-0.4s-after-pulse-2 | drop:within-0.4s-after-pulse-3 | maxStep | success | taskBudget |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 0.6 | light | 33 | 0.42 | 1 | 0 | 9 | 1 | 5 | 0 | 3 | 0 | 0 | 0 | 14 | 0 |
| 0.6 | mid | 33 | 0.48 | 2 | 1 | 8 | 0 | 2 | 1 | 0 | 1 | 1 | 1 | 16 | 0 |
| 0.6 | heavy | 34 | 0.53 | 1 | 1 | 9 | 2 | 2 | 1 | 0 | 0 | 0 | 0 | 18 | 0 |
| 1.0 | light | 33 | 0.45 | 0 | 0 | 13 | 0 | 1 | 1 | 2 | 0 | 0 | 1 | 15 | 0 |
| 1.0 | mid | 33 | 0.52 | 0 | 0 | 11 | 1 | 1 | 1 | 1 | 0 | 0 | 1 | 17 | 0 |
| 1.0 | heavy | 34 | 0.65 | 0 | 0 | 5 | 3 | 1 | 1 | 1 | 0 | 1 | 0 | 22 | 0 |
| 1.5 | light | 33 | 0.42 | 2 | 0 | 13 | 0 | 1 | 1 | 0 | 0 | 0 | 2 | 14 | 0 |
| 1.5 | mid | 33 | 0.30 | 2 | 0 | 11 | 3 | 0 | 1 | 4 | 1 | 0 | 1 | 10 | 0 |
| 1.5 | heavy | 34 | 0.41 | 0 | 0 | 11 | 2 | 0 | 2 | 3 | 1 | 0 | 0 | 14 | 1 |
| pooled | light | 99 | 0.43 | 3 | 0 | 35 | 1 | 7 | 2 | 5 | 0 | 0 | 3 | 43 | 0 |
| pooled | mid | 99 | 0.43 | 4 | 1 | 30 | 4 | 3 | 3 | 5 | 2 | 1 | 3 | 43 | 0 |
| pooled | heavy | 102 | 0.53 | 1 | 1 | 25 | 7 | 3 | 4 | 4 | 1 | 1 | 0 | 54 | 1 |

**1a. Hold-phase drops: hold step at the drop (buckets) and pulses applied by then, by mass tercile (pooled over mu)**

| mass | n | 0-49 | 50-99 | 100-149 | 150-199 | 200-299 | 300-399 | 400-499 | p=0 | p=1 | p=2 | p>=3 | mean | median |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| light | 50 | 32 | 4 | 2 | 1 | 4 | 5 | 2 | 35 | 6 | 7 | 2 | 101.0 | 28.0 |
| mid | 49 | 31 | 4 | 3 | 1 | 2 | 3 | 5 | 30 | 10 | 5 | 4 | 109.5 | 36 |
| heavy | 46 | 20 | 8 | 6 | 3 | 4 | 3 | 2 | 25 | 12 | 4 | 5 | 107.4 | 65.5 |
| all | 145 | 83 | 16 | 11 | 5 | 10 | 11 | 9 | 90 | 28 | 16 | 11 | 105.9 | 35 |

**1a. Hold-phase drops by pulse index (re-run with pulse logging)**

| mu | massTercile | n_drops_hold | medianHoldStepBeforePulse1 | hold-before-pulse-1 | during-pulse-1 | within-0.4s-after-pulse-1 | later-after-pulse-1 | during-pulse-2 | within-0.4s-after-pulse-2 | later-after-pulse-2 | during-pulse-3 | within-0.4s-after-pulse-3 | later-after-pulse-3 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 0.6 | light | 18 | 28 | 9 | 0 | 3 | 1 | 0 | 0 | 5 | 0 | 0 | 0 |
| 0.6 | mid | 14 | 17.5 | 8 | 1 | 0 | 0 | 0 | 1 | 2 | 0 | 1 | 1 |
| 0.6 | heavy | 15 | 23 | 9 | 1 | 0 | 2 | 0 | 0 | 2 | 0 | 0 | 1 |
| 0.6 | all | 47 | 23.5 | 26 | 2 | 3 | 3 | 0 | 1 | 9 | 0 | 1 | 2 |
| 1.0 | light | 17 | 16 | 13 | 0 | 2 | 0 | 0 | 0 | 1 | 0 | 0 | 1 |
| 1.0 | mid | 15 | 31 | 11 | 0 | 1 | 1 | 0 | 0 | 1 | 0 | 0 | 1 |
| 1.0 | heavy | 12 | 28 | 5 | 0 | 1 | 3 | 0 | 0 | 1 | 0 | 1 | 1 |
| 1.0 | all | 44 | 24 | 29 | 0 | 4 | 4 | 0 | 0 | 3 | 0 | 1 | 3 |
| 1.5 | light | 15 | 23 | 13 | 0 | 0 | 0 | 0 | 0 | 1 | 0 | 0 | 1 |
| 1.5 | mid | 20 | 24 | 11 | 0 | 4 | 3 | 0 | 1 | 0 | 0 | 0 | 1 |
| 1.5 | heavy | 19 | 38 | 11 | 0 | 3 | 2 | 0 | 1 | 0 | 0 | 0 | 2 |
| 1.5 | all | 54 | 24 | 35 | 0 | 7 | 5 | 0 | 2 | 1 | 0 | 0 | 4 |
| pooled | light | 50 | 23 | 35 | 0 | 5 | 1 | 0 | 0 | 7 | 0 | 0 | 2 |
| pooled | mid | 49 | 24.5 | 30 | 1 | 5 | 4 | 0 | 2 | 3 | 0 | 1 | 3 |
| pooled | heavy | 46 | 24 | 25 | 1 | 4 | 7 | 0 | 1 | 3 | 0 | 1 | 4 |
| pooled | all | 145 | 24.0 | 90 | 2 | 14 | 12 | 0 | 3 | 13 | 0 | 2 | 9 |

**1b. Contacts at the drop step vs at the end of successful episodes (pooled over mu)**

| mu | mass | group | n | contacts | distinct fingers | palm | thumb |
|---|---|---|---|---|---|---|---|
| pooled | light | success end | 43 | 5.33 | 2.65 | 0.91 | 1.00 |
| pooled | light | drop (hold) | 50 | 2.76 | 1.40 | 0.42 | 0.52 |
| pooled | light | drop (before lift) | 3 | 1.33 | 1.00 | 0.33 | 0.00 |
| pooled | mid | success end | 43 | 4.88 | 2.51 | 0.95 | 0.98 |
| pooled | mid | drop (hold) | 49 | 2.69 | 1.41 | 0.41 | 0.37 |
| pooled | mid | drop (before lift) | 4 | 2.50 | 1.25 | 0.25 | 0.50 |
| pooled | heavy | success end | 54 | 5.46 | 2.72 | 0.98 | 1.00 |
| pooled | heavy | drop (hold) | 46 | 3.93 | 2.11 | 0.59 | 0.67 |
| pooled | heavy | drop (before lift) | 1 | 8.00 | 4.00 | 1.00 | 1.00 |
| pooled | all | success end | 140 | 5.24 | 2.64 | 0.95 | 0.99 |
| pooled | all | drop (hold) | 145 | 3.11 | 1.63 | 0.47 | 0.52 |
| pooled | all | drop (before lift) | 8 | 2.75 | 1.50 | 0.38 | 0.38 |

**Success by theta bin per mu (stiffness edges 1.276 / 1.669 N m/rad)**

| mu | bin | n | success |
|---|---|---|---|
| 0.6 | low-k | 34 | 0.47 |
| 0.6 | mid-k | 32 | 0.56 |
| 0.6 | high-k | 34 | 0.41 |
| 0.6 | mass light | 33 | 0.42 |
| 0.6 | mass mid | 33 | 0.48 |
| 0.6 | mass heavy | 34 | 0.53 |
| 0.6 | groups<=9 | 13 | 0.54 |
| 0.6 | groups 10-11 | 41 | 0.56 |
| 0.6 | groups 12-14 | 46 | 0.39 |
| 1.0 | low-k | 33 | 0.33 |
| 1.0 | mid-k | 33 | 0.70 |
| 1.0 | high-k | 34 | 0.59 |
| 1.0 | mass light | 33 | 0.45 |
| 1.0 | mass mid | 33 | 0.52 |
| 1.0 | mass heavy | 34 | 0.65 |
| 1.0 | groups<=9 | 13 | 0.54 |
| 1.0 | groups 10-11 | 41 | 0.63 |
| 1.0 | groups 12-14 | 46 | 0.46 |
| 1.5 | low-k | 33 | 0.21 |
| 1.5 | mid-k | 33 | 0.45 |
| 1.5 | high-k | 34 | 0.47 |
| 1.5 | mass light | 33 | 0.42 |
| 1.5 | mass mid | 33 | 0.30 |
| 1.5 | mass heavy | 34 | 0.41 |
| 1.5 | groups<=9 | 13 | 0.38 |
| 1.5 | groups 10-11 | 41 | 0.34 |
| 1.5 | groups 12-14 | 46 | 0.41 |

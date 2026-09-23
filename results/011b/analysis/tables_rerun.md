
**1a. Mass tercile (edges 0.386 / 0.762 kg from the mu 1.0 file) x end phase**

| mu | mass | n | success | drop:between-pulses-after-1 | drop:between-pulses-after-2 | drop:between-pulses-after-3 | drop:hold-before-pulse-1 | maxStep | success |
|---|---|---|---|---|---|---|---|---|---|
| 0.6 | light | 33 | 0.45 | 2 | 1 | 1 | 13 | 1 | 15 |
| 0.6 | mid | 33 | 0.52 | 0 | 1 | 1 | 13 | 1 | 17 |
| 0.6 | heavy | 34 | 0.65 | 3 | 0 | 2 | 7 | 0 | 22 |
| 1.0 | light | 33 | 0.45 | 2 | 1 | 1 | 13 | 1 | 15 |
| 1.0 | mid | 33 | 0.52 | 0 | 1 | 1 | 13 | 1 | 17 |
| 1.0 | heavy | 34 | 0.65 | 3 | 0 | 2 | 7 | 0 | 22 |
| 1.5 | light | 33 | 0.45 | 2 | 1 | 1 | 13 | 1 | 15 |
| 1.5 | mid | 33 | 0.52 | 0 | 1 | 1 | 13 | 1 | 17 |
| 1.5 | heavy | 34 | 0.65 | 3 | 0 | 2 | 7 | 0 | 22 |
| pooled | light | 99 | 0.45 | 6 | 3 | 3 | 39 | 3 | 45 |
| pooled | mid | 99 | 0.52 | 0 | 3 | 3 | 39 | 3 | 51 |
| pooled | heavy | 102 | 0.65 | 9 | 0 | 6 | 21 | 0 | 66 |

**1a. Hold-phase drops: hold step at the drop (buckets) and pulses applied by then, by mass tercile (pooled over mu)**

| mass | n | 0-49 | 50-99 | 100-149 | 150-199 | 200-299 | 300-399 | 400-499 | p=0 | p=1 | p=2 | p>=3 | mean | median |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| light | 51 | 36 | 3 | 0 | 0 | 6 | 3 | 3 | 39 | 6 | 3 | 3 | 92.4 | 19 |
| mid | 45 | 33 | 3 | 3 | 0 | 3 | 0 | 3 | 33 | 6 | 3 | 3 | 79.8 | 36 |
| heavy | 36 | 12 | 3 | 3 | 6 | 6 | 3 | 3 | 15 | 12 | 3 | 6 | 154.2 | 126.0 |
| all | 132 | 81 | 9 | 6 | 6 | 15 | 6 | 9 | 87 | 24 | 9 | 12 | 105.0 | 35.0 |

**1a. Hold-phase drops by pulse index (re-run with pulse logging)**

| mu | massTercile | n_drops_hold | before-pulse-1 | during-1 | after-1 | during-2 | after-2 | during-3 | after-3 |
|---|---|---|---|---|---|---|---|---|---|
| 0.6 | light | 17 | 13 | 0 | 2 | 0 | 1 | 0 | 1 |
| 0.6 | mid | 15 | 13 | 0 | 0 | 0 | 1 | 0 | 1 |
| 0.6 | heavy | 12 | 7 | 0 | 3 | 0 | 0 | 0 | 2 |
| 0.6 | all | 44 | 33 | 0 | 5 | 0 | 2 | 0 | 4 |
| 1.0 | light | 17 | 13 | 0 | 2 | 0 | 1 | 0 | 1 |
| 1.0 | mid | 15 | 13 | 0 | 0 | 0 | 1 | 0 | 1 |
| 1.0 | heavy | 12 | 7 | 0 | 3 | 0 | 0 | 0 | 2 |
| 1.0 | all | 44 | 33 | 0 | 5 | 0 | 2 | 0 | 4 |
| 1.5 | light | 17 | 13 | 0 | 2 | 0 | 1 | 0 | 1 |
| 1.5 | mid | 15 | 13 | 0 | 0 | 0 | 1 | 0 | 1 |
| 1.5 | heavy | 12 | 7 | 0 | 3 | 0 | 0 | 0 | 2 |
| 1.5 | all | 44 | 33 | 0 | 5 | 0 | 2 | 0 | 4 |
| pooled | light | 51 | 39 | 0 | 6 | 0 | 3 | 0 | 3 |
| pooled | mid | 45 | 39 | 0 | 0 | 0 | 3 | 0 | 3 |
| pooled | heavy | 36 | 21 | 0 | 9 | 0 | 0 | 0 | 6 |
| pooled | all | 132 | 99 | 0 | 15 | 0 | 6 | 0 | 12 |

**1b. Contacts at the drop step vs at the end of successful episodes (pooled over mu)**

| mu | mass | group | n | contacts | distinct fingers | palm | thumb |
|---|---|---|---|---|---|---|---|
| pooled | light | success end | 45 | 5.60 | 2.87 | 0.93 | 1.00 |
| pooled | light | drop (hold) | 51 | 2.53 | 1.12 | 0.53 | 0.59 |
| pooled | mid | success end | 51 | 4.76 | 2.41 | 0.88 | 0.94 |
| pooled | mid | drop (hold) | 45 | 2.33 | 1.20 | 0.40 | 0.27 |
| pooled | heavy | success end | 66 | 5.36 | 2.77 | 1.00 | 1.00 |
| pooled | heavy | drop (hold) | 36 | 4.92 | 2.83 | 0.75 | 0.75 |
| pooled | all | success end | 162 | 5.24 | 2.69 | 0.94 | 0.98 |
| pooled | all | drop (hold) | 132 | 3.11 | 1.61 | 0.55 | 0.52 |

**Success by theta bin per mu (stiffness edges 1.276 / 1.669 N m/rad)**

| mu | bin | n | success |
|---|---|---|---|
| 0.6 | low-k | 33 | 0.33 |
| 0.6 | mid-k | 33 | 0.70 |
| 0.6 | high-k | 34 | 0.59 |
| 0.6 | mass light | 33 | 0.45 |
| 0.6 | mass mid | 33 | 0.52 |
| 0.6 | mass heavy | 34 | 0.65 |
| 0.6 | groups<=9 | 13 | 0.54 |
| 0.6 | groups 10-11 | 41 | 0.63 |
| 0.6 | groups 12-14 | 46 | 0.46 |
| 1.0 | low-k | 33 | 0.33 |
| 1.0 | mid-k | 33 | 0.70 |
| 1.0 | high-k | 34 | 0.59 |
| 1.0 | mass light | 33 | 0.45 |
| 1.0 | mass mid | 33 | 0.52 |
| 1.0 | mass heavy | 34 | 0.65 |
| 1.0 | groups<=9 | 13 | 0.54 |
| 1.0 | groups 10-11 | 41 | 0.63 |
| 1.0 | groups 12-14 | 46 | 0.46 |
| 1.5 | low-k | 33 | 0.33 |
| 1.5 | mid-k | 33 | 0.70 |
| 1.5 | high-k | 34 | 0.59 |
| 1.5 | mass light | 33 | 0.45 |
| 1.5 | mass mid | 33 | 0.52 |
| 1.5 | mass heavy | 34 | 0.65 |
| 1.5 | groups<=9 | 13 | 0.54 |
| 1.5 | groups 10-11 | 41 | 0.63 |
| 1.5 | groups 12-14 | 46 | 0.46 |

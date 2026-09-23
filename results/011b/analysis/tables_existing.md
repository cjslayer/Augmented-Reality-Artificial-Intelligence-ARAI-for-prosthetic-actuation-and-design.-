
**1a. Mass tercile (edges 0.386 / 0.762 kg from the mu 1.0 file) x end phase**

| mu | mass | n | success | drop:before-lift | drop:hold | maxStep | success |
|---|---|---|---|---|---|---|---|
| 0.6 | light | 33 | 0.64 | 2 | 8 | 2 | 21 |
| 0.6 | mid | 33 | 0.55 | 2 | 13 | 0 | 18 |
| 0.6 | heavy | 34 | 0.59 | 1 | 13 | 0 | 20 |
| 1.0 | light | 33 | 0.39 | 1 | 18 | 1 | 13 |
| 1.0 | mid | 33 | 0.55 | 2 | 13 | 0 | 18 |
| 1.0 | heavy | 34 | 0.59 | 0 | 14 | 0 | 20 |
| 1.5 | light | 33 | 0.55 | 1 | 14 | 0 | 18 |
| 1.5 | mid | 33 | 0.48 | 0 | 16 | 1 | 16 |
| 1.5 | heavy | 34 | 0.53 | 1 | 15 | 0 | 18 |
| pooled | light | 99 | 0.53 | 4 | 40 | 3 | 52 |
| pooled | mid | 99 | 0.53 | 4 | 42 | 1 | 52 |
| pooled | heavy | 102 | 0.57 | 2 | 42 | 0 | 58 |

**1a. Hold-phase drops: hold step at the drop (buckets) and pulses applied by then, by mass tercile (pooled over mu)**

| mass | n | 0-49 | 50-99 | 100-149 | 150-199 | 200-299 | 300-399 | 400-499 | p=0 | p=1 | p=2 | p>=3 | mean | median |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| light | 40 | 24 | 6 | 2 | 2 | 3 | 0 | 3 | 25 | 7 | 3 | 5 | 85.9 | 33.0 |
| mid | 42 | 20 | 7 | 2 | 2 | 5 | 3 | 3 | 25 | 8 | 4 | 5 | 116.0 | 51.0 |
| heavy | 42 | 26 | 6 | 1 | 2 | 4 | 1 | 2 | 28 | 10 | 2 | 2 | 91.0 | 37.0 |
| all | 124 | 70 | 19 | 5 | 6 | 12 | 4 | 8 | 78 | 25 | 9 | 12 | 97.8 | 37.0 |

**1b. Contacts at the drop step vs at the end of successful episodes (pooled over mu)**

| mu | mass | group | n | contacts | distinct fingers | palm | thumb |
|---|---|---|---|---|---|---|---|
| pooled | light | success end | 52 | 4.94 | 2.52 | 0.96 | 0.96 |
| pooled | light | drop (hold) | 40 | 2.42 | 1.23 | 0.40 | 0.53 |
| pooled | light | drop (before lift) | 4 | 3.00 | 1.00 | 0.75 | 0.50 |
| pooled | mid | success end | 52 | 5.27 | 2.46 | 0.98 | 0.98 |
| pooled | mid | drop (hold) | 42 | 2.90 | 1.55 | 0.52 | 0.52 |
| pooled | mid | drop (before lift) | 4 | 2.00 | 1.00 | 0.75 | 0.25 |
| pooled | heavy | success end | 58 | 5.09 | 2.59 | 0.91 | 1.00 |
| pooled | heavy | drop (hold) | 42 | 3.33 | 1.62 | 0.45 | 0.50 |
| pooled | heavy | drop (before lift) | 2 | 6.00 | 2.00 | 0.00 | 1.00 |
| pooled | all | success end | 162 | 5.10 | 2.52 | 0.95 | 0.98 |
| pooled | all | drop (hold) | 124 | 2.90 | 1.47 | 0.46 | 0.52 |
| pooled | all | drop (before lift) | 10 | 3.20 | 1.20 | 0.60 | 0.50 |

**Success by theta bin per mu (stiffness edges 1.276 / 1.681 N m/rad)**

| mu | bin | n | success |
|---|---|---|---|
| 0.6 | low-k | 34 | 0.56 |
| 0.6 | mid-k | 33 | 0.67 |
| 0.6 | high-k | 33 | 0.55 |
| 0.6 | mass light | 33 | 0.64 |
| 0.6 | mass mid | 33 | 0.55 |
| 0.6 | mass heavy | 34 | 0.59 |
| 0.6 | groups<=9 | 13 | 0.77 |
| 0.6 | groups 10-11 | 41 | 0.59 |
| 0.6 | groups 12-14 | 46 | 0.54 |
| 1.0 | low-k | 33 | 0.48 |
| 1.0 | mid-k | 33 | 0.67 |
| 1.0 | high-k | 34 | 0.38 |
| 1.0 | mass light | 33 | 0.39 |
| 1.0 | mass mid | 33 | 0.55 |
| 1.0 | mass heavy | 34 | 0.59 |
| 1.0 | groups<=9 | 13 | 0.38 |
| 1.0 | groups 10-11 | 41 | 0.56 |
| 1.0 | groups 12-14 | 46 | 0.50 |
| 1.5 | low-k | 33 | 0.55 |
| 1.5 | mid-k | 34 | 0.50 |
| 1.5 | high-k | 33 | 0.52 |
| 1.5 | mass light | 33 | 0.55 |
| 1.5 | mass mid | 33 | 0.48 |
| 1.5 | mass heavy | 34 | 0.53 |
| 1.5 | groups<=9 | 13 | 0.38 |
| 1.5 | groups 10-11 | 41 | 0.54 |
| 1.5 | groups 12-14 | 46 | 0.54 |

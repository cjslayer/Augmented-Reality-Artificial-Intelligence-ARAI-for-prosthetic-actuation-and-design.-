| head | mu | n | success [95 % Wilson] | lifted | palm at end | contacts | mean hold steps | ends |
|---|---|---|---|---|---|---|---|---|
| deterministic | 0.6 | 300 | 0.97 [0.94, 0.98] | 0.97 | 0.96 | 5.56 | 484 | {'drop': 6, 'maxStep': 4, 'success': 290} |
| deterministic | 1.0 | 300 | 0.98 [0.96, 0.99] | 0.99 | 0.96 | 5.51 | 490 | {'drop': 5, 'maxStep': 1, 'success': 294} |
| deterministic | 1.5 | 300 | 0.99 [0.97, 1.00] | 0.99 | 0.94 | 5.62 | 495 | {'drop': 2, 'maxStep': 1, 'success': 297} |
| sampled | 1.0 | 300 | 0.92 [0.88, 0.95] | 0.99 | 0.89 | 4.42 | 468 | {'drop': 23, 'maxStep': 1, 'success': 276} |

Deterministic minus sampled at mu 1.0: +0.060 (95 % CI +0.025 to +0.095); note the two passes use the same seeds but theta-pairing holds only within the same mu/passIndex hash, which it does here (same mu, same passIndex).
theta identical between the two mu 1.0 passes: 300/300 rows

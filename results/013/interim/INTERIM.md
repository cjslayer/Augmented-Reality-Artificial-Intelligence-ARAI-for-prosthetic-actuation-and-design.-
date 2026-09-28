# Run 013 interim checks at 12.25M (2026-09-28 09:00, training not stopped or altered)

## 1. Canonical forgetting check

Checkpoint `results/013/Prosthetic/Prosthetic-12249962.onnx` (phase C: canonical fraction 0 since 2.95M, perturbation 1.0 since 7.44M),
Editor harness, deterministic head, one job worker, canonical pose, mu 1.0, K = 50, perturbation 1.0, mass as 012, throwaway pass first.
Train shapes stratified on seeds 9001-9128 (2 episodes per shape) and held-out shapes on 9129-9192 (4 per shape), passIndex 0: every
row has the same seed, shape and theta as the corresponding row of the v2 012 canonical passes (`results/013_prep/passes_v2/`
train_canonical_9001_9300_p0 rows 1-128, heldout_canonical_9001_9300_p0 rows 129-192; 192 of 192 matched). Passes: `interim_*.csv`,
log `passes.json`.

| subset | 013 @ 12.25M | 012 (same rows) | paired 013 − 012 [95 % CI] |
|---|---|---|---|
| all 192 | 0.026 [0.011, 0.060] (n=192) | 0.615 [0.544, 0.681] (n=192) | -0.589 [-0.663, -0.515] |
| train shapes (1-64), 128 | 0.023 [0.008, 0.067] (n=128) | 0.633 [0.547, 0.711] (n=128) | -0.609 [-0.699, -0.519] |
| held-out shapes (65-80), 64 | 0.031 [0.009, 0.107] (n=64) | 0.578 [0.456, 0.691] (n=64) | -0.547 [-0.676, -0.417] |
| length < 130 mm | 0.048 [0.017, 0.133] (n=62) | 0.081 [0.035, 0.175] (n=62) | -0.032 [-0.121, +0.057] |
| length 130-210 mm | 0.022 [0.006, 0.076] (n=92) | 0.837 [0.748, 0.899] (n=92) | -0.815 [-0.895, -0.736] |
| length 210-280 mm | 0.000 [0.000, 0.092] (n=38) | 0.947 [0.827, 0.985] (n=38) | -0.947 [-1.018, -0.876] |
013 ends {'success': 5, 'reach': 37, 'drop': 139, 'liftBudget': 11} | drop phases {'lift phase': 117, 'between pulses': 6, 'before first pulse': 12, 'during pulse': 3, 'after last pulse': 1} | gate 0.807 lifted 0.146 palm 0.271 thumb 0.625 contacts 4.31
012 ends {'success': 118, 'reach': 48, 'drop': 26} | drop phases {'between pulses': 6, 'lift phase': 15, 'before first pulse': 5} | gate 0.750 lifted 0.677 palm 0.844 thumb 0.828 contacts 3.96

For reference, 012's full canonical measurement (`ZERO_SHOT_v2.md`, 900 train episodes) by length was 0.17 / 0.82 / 0.93; on these
192 paired rows it is 0.08 / 0.84 / 0.95.

How 013 fails on upright objects (192 paired episodes; 012 in parentheses):

| quantity | 013 @ 12.25M | 012 |
|---|---|---|
| gate reached | 0.807 | 0.750 |
| lifted | 0.146 | 0.677 |
| palm touching at end | 0.271 | 0.844 |
| thumb touching at end | 0.625 | 0.828 |
| ends | 5 success, 139 drop (117 in the lift phase), 37 reach failures, 11 lift-budget | 118 success, 26 drop, 48 reach failures |

Anchor cylinder (the throwaway, seeds 6091-6100, canonical): 0 of 9 counted episodes succeed (7 drops, most at hold step 0, and 2
reach failures); 012 scores 10 of 10 on the same seeds.

Reading: the 12.25M policy has forgotten the upright grasp. It still meets the contact gate on a standing object (0.81, more often than
012), but it does so without the palm (0.27 against 0.84) and loses the object in the lift phase in 117 of 192 episodes: the
approach it has learned for lying objects, closing from above without a palm press, tips or releases a standing one. The loss is
largest where 012 is best (210-280 mm bars: 0.95 → 0.00; 130-210 mm: 0.84 → 0.02) and nil only on the short pucks nobody grasps.
This is catastrophic forgetting of the phase-A/B presentation after 9.3M steps with canonical fraction 0 (phase B ended at 2.95M).
It does not trigger a stop rule of the brief, but it bears directly on the deployment question and on the plateau-stop choice of
"best checkpoint by held-out canonical + lying mean".

Throughput during the check: the trainer alternates about 300 and 460 steps/s between 10k-step summaries (the same rhythm is present
before the pass began, it is the policy-update cadence); averaged over the 4-minute pass window it ran at about 345-350 steps/s against
about 372 in the 4 minutes before, a drop of about 6 % that touched the 350 line. The check ran now rather than at a monitor gap because
the brief allowed that when throughput stays at or above 350; it sat at the line, so any further Editor checks during training
would be better scheduled at a switch gap.

## 2. min_lesson_length / reward-buffer investigation (Part 1a, `results/013_prep/CURRICULUM_NOTE.md`)

- The reward buffer IS cleared at every lesson change, `ml-agents/mlagents/trainers/trainer_controller.py:215-220`:

  ```python
  updated, param_must_reset = self.param_manager.update_lessons(curr_step, max_step, reward_buff)
  if updated:
      for trainer in self.trainers.values():
          trainer.reward_buffer.clear()
  ```

- `min_lesson_length` counts episodes (entries in the reward buffer), `settings.py:489-515`:

  ```python
  # Is the min number of episodes reached
  if len(reward_buffer) < self.min_lesson_length:
      return False, smoothing
  ...
  measure = np.mean(reward_buffer)
  if self.signal_smoothing:
      measure = 0.25 * smoothing + 0.75 * measure
  if measure > self.threshold:
      return True, smoothing
  ```

  With episodes of 40-90 decisions, 100 episodes are 4-9k trainer steps, and the smoothed value is carried across lessons, so a ladder
  whose lessons share a threshold the policy already meets advances every ~100 episodes. That is 012's "cascade"; the premise that the
  buffer is not cleared does not hold.
- `require_reset` (`environment_parameter_manager.py:184-185`, `trainer_controller.py:224-225`) only resets the environments at the
  change; it does not alter the episode count, the threshold test or the buffer clearing.

Run 013's own lesson changes (episodes estimated from the logged episode lengths):

| change | steps | episodes | mean summary reward | criteria |
|---|---|---|---|---|
| A Hold20 → Hold50 | 30k (790k → 820k) | ~650 | 0.77 | min 100, no require_reset |
| B Canon100 → Canon50 | 160k (1.00M → 1.16M) | ~3,000 | 0.87 | min 1000, require_reset |
| B Canon50 → Canon0 | 1.79M (1.16M → 2.95M) | ~24,000 | 0.68 | min 1000, require_reset |
| C Perturb25 → Perturb50 | 4.41M (3.0M → 7.41M) | ~92,000 | 0.60 | min 100, no require_reset |
| C Perturb50 → Perturb100 | 30k (7.41M → 7.44M) | ~650 | 0.80 | min 100, no require_reset |

Did require_reset change the cascade at 7.41M? It could not: the phase-C yaml copies 012's criteria ("same thresholds as 012"), which
have no require_reset, and Perturb50 → Perturb100 took 30k steps, the same cascade as 012. In phase B, where require_reset was set, no
cascade occurred, but the dwell times (3,000 and 24,000 episodes) were set by the 0.9 reward threshold, not by require_reset or the
1000-episode minimum (about 70k steps).

## 3. GRID_012_canonical.md: headline table and mechanical paragraph (from results/013_prep/GRID_012_canonical.md, commit 7b3f8ca)

| hand | thumb base | episodes | success [95 % Wilson] | lifted | palm at end | thumb at end | contacts | reach fail | drops (lift phase / before first pulse / during / between / after) | gate→lift median steps |
|---|---|---|---|---|---|---|---|---|---|---|
| 5 | masked | 399 | 0.531 [0.482, 0.580] (n=399) | 0.617 | 0.820 | 0.732 | 3.55 | 0.253 | 54 / 18 / 2 / 9 / 3 | 14.0 |
| 11 | masked | 399 | 0.441 [0.393, 0.490] (n=399) | 0.526 | 0.777 | 0.717 | 3.23 | 0.383 | 37 / 17 / 1 / 12 / 3 | 12.0 |
| 14 | masked | 398 | 0.603 [0.554, 0.650] (n=398) | 0.711 | 0.759 | 0.741 | 3.86 | 0.201 | 37 / 15 / 0 / 23 / 3 | 14 |
| 1 | active | 399 | 0.619 [0.570, 0.665] (n=399) | 0.684 | 0.802 | 0.827 | 4.18 | 0.266 | 21 / 12 / 0 / 9 / 4 | 11 |
| 2 | active | 399 | 0.692 [0.645, 0.735] (n=399) | 0.752 | 0.852 | 0.870 | 4.45 | 0.211 | 16 / 11 / 2 / 7 / 3 | 13.0 |
| 3 | active | 400 | 0.620 [0.572, 0.666] (n=400) | 0.728 | 0.710 | 0.745 | 3.93 | 0.140 | 55 / 26 / 0 / 12 / 3 | 12 |

Paired over 398 episodes common to all six hands (same seed, passIndex and shape): thumb-masked mean 0.524, easy mean 0.643, **thumb-masked − easy -0.119 [-0.146, -0.092]**.

The thumb-base deficit is real on upright bank objects and larger than on the cylinder: paired over 398 episodes the thumb-masked hands
succeed 0.524 against 0.643 for the easy hands, −0.119 [−0.146, −0.092] (on the anchor cylinder at the same perturbation level the gap
was a few points: 0.91-0.98 against 1.00, `results/bo/partB/probe.csv`). It depends on the object in a way that follows what the thumb has
to do. On long bars (210-280 mm, aspect ≥ 3) the fingers can clamp the object against the palm along its length and the missing thumb
drive matters little: −0.035 [−0.079, +0.008] and −0.067, with hand 14 at 0.99 on the long bars. On short pucks (< 130 mm, aspect < 1.5)
nobody succeeds (0.04-0.22), so there is nothing to lose. The gap opens on mid-length objects (130-210 mm: −0.205 [−0.249, −0.161];
aspect 1.5-3: −0.173), where the fingers alone cannot span enough of the object to wrap it and a driven thumb has to oppose them
across the cross-section, and on the rounded-diamond cross-sections (e2 1.1-1.5: −0.179), whose edges need opposition rather than a
palm press. The mechanism in the logged columns is the one of `HARD_HANDS.md`: at the end of an episode the thumb touches the object in
72-74 % of the thumb-masked episodes against 75-87 % for the easy hands, they hold fewer segments (3.2-3.9 against 3.9-4.5 finger groups),
and they reach the gate less often (reach-failure rate 0.20-0.38 against 0.14-0.27). Hand 11 is the weakest (0.44), consistent with its
two further masked groups (ring MCP and pinky DIP besides the thumb base). One easy hand, 3, loses as many objects in the lift phase as
hand 5 (55 and 54 lift-phase drops), so the easy-hand mean is not uniform either. For a morphology objective on the bank, the thumb-base
bit therefore has a shape-dependent effect of 0.04 to 0.21, concentrated on mid-length objects: range that did not exist on the cylinder.

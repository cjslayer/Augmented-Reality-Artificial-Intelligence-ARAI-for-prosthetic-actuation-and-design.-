# Why run 012's perturbation ladder cascaded (read-only investigation, 2026-09-27)

Trainer: the source build in `C:\Users\chris\ml-agents` (develop, commit 3ecb446), `mlagents 1.2.0.dev0`. No trainer file was changed.

## What the code does

- **The reward buffer is cleared at every lesson change.** `trainer_controller.py:208-227`, `reset_env_if_ready` (clear at `:220`):

  ```python
  reward_buff = {k: list(t.reward_buffer) for (k, t) in self.trainers.items()}
  ...
  updated, param_must_reset = self.param_manager.update_lessons(curr_step, max_step, reward_buff)
  if updated:
      for trainer in self.trainers.values():
          trainer.reward_buffer.clear()
  ```

  The premise of the brief ("reward buffer not cleared at lesson change") does not hold for this trainer.
- **`min_lesson_length` counts episodes, not steps.** `settings.py:489-515`, `CompletionCriteriaSettings.need_increment`:

  ```python
  # Is the min number of episodes reached
  if len(reward_buffer) < self.min_lesson_length:
      return False, smoothing
  ...
  measure = np.mean(reward_buffer)
  if self.signal_smoothing:
      measure = 0.25 * smoothing + 0.75 * measure
      smoothing = measure
  if measure > self.threshold:
      return True, smoothing
  ```

  The buffer holds one entry per finished episode (`rl_trainer.py:81`, `self.reward_buffer.appendleft(rewards.get(agent_id, 0))`) and is a
  deque capped at the largest `min_lesson_length` of the run's lessons (`trainer.py:45`, `trainer_factory.py:99`,
  `environment_parameter_manager.py:62-78`). The smoothed value (`self._smoothed_values[param_name]`,
  `environment_parameter_manager.py:177`) is not reset at a lesson change.
- **`require_reset` only resets the environments.** `environment_parameter_manager.py:184-185` sets `must_reset`, and
  `trainer_controller.py:224-225` then calls `_reset_env` (all envs restart with the new parameters) and `end_trainer_episodes`, which zeroes
  the per-agent running returns without appending them (`rl_trainer.py:61-68`). It does not change the episode count needed, the
  threshold test or the buffer clearing; its only effect is that the episodes counted for the next lesson all started under the new
  parameters (without it, the up to 8 episodes in flight at the change finish under the old ones and count toward the new lesson).

## Why run 012 cascaded

`results/012/Prosthetic/events.out.tfevents.1790146607...` (phase B), `Environment/Lesson Number/perturb/scale` and
`Environment/Episode Length` per 10k-step summary:

| step | lesson | episode length (decisions) | cumulative reward |
|---|---|---|---|
| 2.67M | 0 (Perturb0) | 63 | 0.81 |
| 2.68M | 1 (Perturb25) | 87 | 0.62 |
| 2.74M | 2 (Perturb50) | 66 | 0.91 |
| 2.77M | 4 (Perturb100) | 66 | 0.89 |

Episodes were 63-92 decisions long, and a trainer step is one decision, so 100 episodes are about 7-9k trainer steps. Every
perturbation lesson had the same threshold (0.9), the policy's smoothed reward was already around it, and the smoothed value carries
over, so each lesson completed as soon as 100 fresh episodes had arrived: Perturb50 and Perturb75 were both passed inside one 10k-step
summary window. `min_lesson_length` worked as written; 100 episodes is simply a short dwell for this task, and equal thresholds on a
ladder whose lessons barely change the reward let it advance at that minimum rate.

## Consequence for run 013

- Phase B (`Config/run_013_phaseB.yaml`) uses `min_lesson_length: 1000` (about 80k steps at 012's episode lengths) with
  `require_reset: true`; the brief's floor was 100. A lying presentation lowers the reward sharply, so the 0.9 threshold is also a real
  gate there, unlike on 012's perturbation ladder.
- Phase C (`Config/run_013_phaseC.yaml`) keeps 012's `min_lesson_length: 100` and thresholds as specified ("same thresholds as 012"),
  so its perturbation ladder can cascade the same way.
- No trainer patch was made.

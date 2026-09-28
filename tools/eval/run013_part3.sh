#!/bin/bash
# Run 013 Part 3 evaluation sequence (brief + amendment), run on an idle machine after training (no trainer / player processes).
# Every pass: Editor harness, deterministic head, one job worker, per-episode reseed, K = 50, mu 1.0, mass as 012; perturbation 1.0 unless noted.
# Models: 012 = results/012/Prosthetic.onnx; 013C = results/013/013C/Prosthetic.onnx (Prosthetic-13499981); 013D = results/013/013D/Prosthetic.onnx (20M).
# Output: results/013/eval/<model>/ with passes.json per model; log results/013/eval/part3.log.
set -u
cd /c/Users/chris/Augmented-Reality-Artificial-Intelligence
PY=/c/Users/chris/ml-agents/venv/Scripts/python.exe
RP="$PY -m tools.eval.run_passes"
E=results/013/eval
declare -A MODEL=([012]=results/012/Prosthetic.onnx [013C]=results/013/013C/Prosthetic.onnx [013D]=results/013/013D/Prosthetic.onnx)
step() { echo "=== $(date +%H:%M:%S) $*"; }
if tasklist | grep -qi -e "mlagents-learn" -e "Prosthetic.exe"; then echo "trainer / players still running: refusing (idle-machine rule)"; exit 1; fi
# 0. load-sensitivity test: the 012 throwaway on an idle machine must reproduce the byte-stable hash 2676025693a9c0e2
step "idle throwaway 012"; $RP throwaway --model ${MODEL[012]} --out $E/idle_check --log $E/idle_check/passes.json || exit 1
# 1. held-out shapes 65-80 x seeds 10001-10300 x passIndex 0-1, both presentations, three models (paired per episode)
for m in 012 013C 013D; do step "held-out $m"; $RP throwaway eval_heldout --model ${MODEL[$m]} --out $E/$m --log $E/$m/passes.json || exit 1; done
# 2. 013D on train shapes 1-64 x seeds 10001-10300 x passIndex 0-2, both presentations
step "train shapes 013D"; $RP eval_train --model ${MODEL[013D]} --out $E/013D --log $E/013D/passes.json || exit 1
# 3. anchor regression, canonical cylinder, seeds 7001-7300: 013C and 013D
for m in 013C 013D; do step "anchor $m"; $RP anchor_canonical --model ${MODEL[$m]} --out $E/$m --log $E/$m/passes.json || exit 1; done
# 4. hand x shape grids on 013D, canonical and random-stable, six hands, seeds 9301-9400
step "grids 013D"; $RP grid_canonical grid_rs --model ${MODEL[013D]} --out $E/013D_grid --log $E/013D_grid/passes.json || exit 1
# 5. BO range probes at the training level (perturbation 1.0): 013D random-stable and canonical, 012 canonical; 20 probe theta, held-out shapes, n = 100
step "range probe 013D randomStable"; $RP range_probe --model ${MODEL[013D]} --out $E/probe_013D_rs --log $E/probe_013D_rs/passes.json --perturb 1.0 || exit 1
step "range probe 013D canonical"; $RP range_probe_canonical --model ${MODEL[013D]} --out $E/probe_013D_can --log $E/probe_013D_can/passes.json --perturb 1.0 || exit 1
step "range probe 012 canonical"; $RP range_probe_canonical --model ${MODEL[012]} --out $E/probe_012_can --log $E/probe_012_can/passes.json --perturb 1.0 || exit 1
step "PART3 DONE"

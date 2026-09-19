## Observations (descriptive; patterns only)

The 20-episode objective saturates: 31 of the 68 feasible BO candidates and 4 of the 58 feasible random candidates score
20/20 on seeds 4001-4020, the first BO proposal (#31) already sits at the ceiling and the random arm reaches it at draw #3,
so the best-so-far curves coincide from candidate 31 on and the optimization block cannot rank the top candidates; the
arms separate in the bulk instead (BO-iteration mean 0.953 over 53 feasible proposals versus 0.776 for the 29 random
initial draws and 0.774 for the random arm; 62/68 BO candidates at or above the anchor's 0.750 versus 41/58 random) and in
the confirmation block (BO top 5: 0.988-1.000 at mu 1.0, mean 0.993; random top 5: 0.921-1.000, mean 0.965; at mu 0.6
0.979 versus 0.928). Both arms contain a candidate that confirms at 1.000 on all 80 fresh episodes (BO #31 and #37,
random #3), and every confirmed candidate keeps success at 79-80/80, so the confirmation differences are drop-test
differences, not grasp failures. The BO top 5 are one family rather than five designs: pairwise normalized distances
0.3-0.7 with Hamming distances 1-4 (random top 5: 2.7-3.5 and 2-7), 58 of the 70 BO proposals came from the local pool,
and all five share indexMiddle and ringBase masked, index scale 0.80-0.86 and thumb scale 0.83-0.87 with the middle,
ring and pinky scales at 1.05-1.20, active-group omega 27-30 rad/s, zeta 0.71-0.76, wrist omega 12-15 rad/s. The random
top 5 have thumb scales 0.91-1.10 and wrist omega 13-23, so the short-thumb / short-index pattern is where the optimizer
converged, not a property shared by all high scorers; within the random arm alone the objective is flat in thumb scale
(0.77 / 0.75 / 0.82 for < 0.9 / 0.9-1.1 / >= 1.1) and rises mildly with active-group count (0.69 at 10 to 0.82 at 13).
The GP's block lengthscales rank the link scales (3.2) and the mask (2.4 Hamming) as the directions along which the
objective varies fastest and omega / zeta / inertia (about 7) as nearly flat. Palm contact stays at zero for every
candidate in both arms (maximum 0.05 in the optimization block, 0.01 in confirmation): the pinch grip of run 010 is
unchanged across the explored morphologies, including the short-fingered winners. Oracle infeasibility (37 % overall)
concentrates in candidates with fewer active groups (10.0 versus 10.9), fewer active thumb groups (1.45 versus 1.88) and
with ringBase, thumbBase or indexMiddle masked; feasibility rises from about half at 8-10 active groups to 90 % at 13 and
100 % at 14.

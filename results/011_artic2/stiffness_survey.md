# Human finger joint rotational stiffness — literature survey (2026-09-22, read-only subagent, web sources)

Purpose: anchor the physical-units stiffness range of theta (spike 2, branch articulated-hand). Units N m/rad.
Conversion used where a paper reports fingertip stiffness: K_joint ~ K_tip * L^2 (small angle, single joint, force
perpendicular to the finger). Levers: MCP->tip 0.082 m (Jindrich 2004 mean), PIP->tip 0.055 m, DIP->tip 0.025 m.
Converted numbers are marked "conv."; numbers the agent could not fetch are marked "unverified".

## Base joint (MCP, index; other fingers assumed similar, no per-finger data found)

| quantity | value | source |
|---|---|---|
| passive, mid-range | 0.025-0.058 (n=3) | Esteki & Mansour 1996, J Biomech 29:443, doi:10.1016/0021-9290(95)00081-x (as tabulated in Frontiers Bioeng 2020, doi:10.3389/fbioe.2020.592637) |
| passive, whole ROM | 0.016-0.21 (n=10) | Kuo & Deshpande 2012, J Biomech 45:2531, doi:10.1016/j.jbiomech.2012.07.034 |
| passive, near end-range (-50 deg) | ~0.4 N m -> ~0.46 secant (conv.) | Kuo & Deshpande 2010 BioRob |
| relaxed / intermediate contraction | 0.13 / ~0.65 | Becker & Mote 1990, as cited in Jindrich et al. 2004 |
| active, tip 2-20 N | K_tip = 40.9 F + 92.5 N/m -> 174-910 N/m -> 1.2-6.1 (conv.) | Hajian & Howe 1997, J Biomech Eng 119:109, doi:10.1115/1.2796052; fit from Friedman & Flash 2007 |
| active, 20 % MVC (~10 N), static | K_tip 970 N/m -> ~6.5 (conv.); flexed IP posture lowers MCP K by 64 % | Milner & Franklin 1998, IEEE TBME 45:1363, doi:10.1109/10.725333 |
| dynamic tapping (0.7-1.2 N) | 0.45-0.71 (loading phase) | Jindrich et al. 2004, J Biomech 37:1589, doi:10.1016/j.jbiomech.2004.01.001 |
| **recommended simulation range** | **passive 0.02-0.2; active 0.5-6; log-uniform** | |

## Middle joint (PIP)

| quantity | value | source |
|---|---|---|
| passive, mid-range, 89 subjects | mean 0.05 N cm/deg = 0.029; larger fingers / men stiffer | Dionysian et al. 2005, J Hand Surg 30:573, doi:10.1016/j.jhsa.2004.10.010 |
| passive, +-20 deg about equilibrium | ~0.023 (MCP 60 deg) / ~0.04 (MCP 0 deg) (conv.) | Kamper data as cited in Qin et al.; Li et al. 2006, J Orthop Res 24:407 |
| active, tapping | 0.45-0.71 | Jindrich 2004 (MCP/PIP/DIP column order ambiguous) |
| active, 10 N tip, all compliance at PIP | ~2.9 (conv., upper bound) | Milner & Franklin 1998 |
| **recommended** | **passive 0.015-0.08; active 0.3-3** | |

## Tip joint (DIP): no direct passive measurement found

| quantity | value | source |
|---|---|---|
| active, tapping | ~0.45-0.54 | Jindrich 2004 |
| passive | none found; thumb IP assumed 0.05 in a model | Wu, Li, Cutlip, An 2009, BioMed Eng OnLine 8:41, doi:10.1186/1475-925X-8-41 |
| **recommended** | **passive 0.01-0.05; active 0.1-1 (scaled from PIP, unverified)** | |

## Thumb (CMC / MCP / IP)

| quantity | value | source |
|---|---|---|
| model-assumed passive | IP 0.05, MCP 0.10, CMC 0.15 (assumption, uncited) | Wu et al. 2009 |
| CMC in vitro, end-range linear region | 5-9 N m/rad; ROM at 1 N m 26-47 deg | Kalshoven et al. 2024, J Biomech 168:112129 |
| active | no in-vivo joint values found | |
| **recommended** | **passive CMC 0.1-0.5, MCP 0.05-0.3, IP 0.03-0.15; active 2-3x finger MCP (unverified)** | |

## Damping
- Jindrich 2004 tapping: b = 0.0017-0.0031 N m s/rad (all three joints); stiffness-dominated response.
- Hajian & Howe 1997: damping ratio in extension ~1.7x that in abduction; absolute values not retrieved (paywalled).
- Recommendation: zeta ~ 0.3-1.0, b = 2 zeta sqrt(k I).

## Max joint torque (context)
- Index MCP flexion 110.7 +- 9 N at the mid-proximal phalanx (Li et al. 2003, Clin Biomech 18, doi:10.1016/s0268-0033(03)00178-5) -> ~2.5-3 N m (conv.).
- Fingertip palmar 27.9 +- 4.1 N (Valero-Cuevas et al. 1998, J Biomech, doi:10.1016/s0021-9290(98)00082-7) -> MCP ~2.3, PIP ~1.5, DIP ~0.7 N m (conv.).
- Chao et al. 1980: up to 1.0 N m MCP, 0.6 N m PIP in daily activities (search snippet, unverified).
- Thumb: pinch 50-100 N x 2-3 cm lever -> 1-3 N m (unverified).

## Caveats
1. Passive values are mid-range tangent stiffnesses; the torque-angle curve is double-exponential, end-range stiffness is 5-20x higher.
2. Passive MCP stiffness depends strongly on wrist and IP posture (Knutson et al. 2000; Li 2006); values assume a neutral wrist.
3. Active values scale roughly linearly with force (Hajian & Howe); steady-state values with reflexes (Milner & Franklin) are ~2x the 20 ms values.
4. Fingertip -> joint conversions put all compliance at one joint and assume small displacements; cross-coupling terms are dropped.
5. Damping data are thin (one tapping study); treat zeta as a free parameter.
6. No per-finger (middle / ring / little) or DIP passive data found; Dionysian shows stiffness scales with finger size.
7. All of the above is for a human-size hand. The ARAI mesh is about 2.5x human length scale (see mesh_measure2.csv); stiffness and torque scale with length^3 for geometrically similar muscle-driven limbs.

# PhysX / Unity 6 contact-solver notes for the articulated hand (2026-09-22, read-only subagent, docs + forum)

## 1. Solver type (PGS vs TGS)
- Unity Physics settings: TGS "offers a better convergence and a better handling of high-mass ratios, minimizes energy introduced when correcting penetrations and improves the resistance of joints to overstretch"; PGS is the default. https://docs.unity3d.com/6000.2/Documentation/Manual/class-PhysicsManager.html
- PhysX guide: TGS = "dramatically improved convergence, improved handling of high-mass ratios, minimizes energy introduced when correcting penetrations, improved joint drive accuracy"; "generally a little slower than PGS" (friction solved every iteration). https://nvidiagameworks.github.io/PhysX/4.1/documentation/physxguide/Manual/RigidBodyDynamics.html
- Under TGS "the number of substeps is equal to the number of position iterations": more position iterations = solver substepping. https://nvidia-omniverse.github.io/PhysX/physx/5.4.1/docs/RigidBodyDynamics.html
- No runtime API in Unity 6 (`Physics.defaultSolverType` does not exist); project-wide `m_SolverType` in DynamicsManager.asset. https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Physics.html
- Applies to articulations (scene-level PxSceneDesc::solverType); articulation joint friction "may appear weaker for eTGS". https://nvidia-omniverse.github.io/PhysX/physx/5.5.0/docs/Articulations.html
- Regression reports: joints exploding under TGS in 2019.3 (forum), ConfigurableJoint accuracy far from the origin (issue tracker).

## 2. Solver iterations
- `Rigidbody.solverIterations` / `ArticulationBody.solverIterations` override the project defaults (6 / 1). PhysX: the counts are lower bounds; "each solver island performs as many iterations as the actor with the highest iteration count requests" -> for a link-rigidbody contact the MAX across the island wins; articulation counts apply to the whole articulation. https://nvidia-omniverse.github.io/PhysX/physx/5.4.1/docs/Simulation.html
- Isaac Gym: raise position iterations only; velocity iterations can hurt convergence. https://docs.robotsfan.com/isaacgym/programming/tuning.html

## 3. Contact offset / rest offset
- Unity exposes `Collider.contactOffset` (contact generation distance, must be > 0; default 0.01 m) and no restOffset (PhysX default 0). Penetration depth is measured against restOffset, not contactOffset. https://docs.unity3d.com/6000.2/Documentation/ScriptReference/Collider-contactOffset.html
- PhysX: contactOffset must exceed restOffset so the solver can "predictively enforce the contact constraint even when the objects are slightly separated"; keep the difference small. https://nvidiagameworks.github.io/PhysX/4.1/documentation/physxguide/Manual/AdvancedCollisionDetection.html

## 4. Max depenetration velocity
- `Physics.defaultMaxDepenetrationVelocity` 10 m/s: "the velocity that the solver can set to a body while trying to pull it out of overlap"; also per `Rigidbody` and per `ArticulationBody`. https://docs.unity3d.com/6000.2/Documentation/ScriptReference/ArticulationBody-maxDepenetrationVelocity.html
- PhysX lists it as a remedy for unstable contact configurations (lower = less explosive correction, more residual overlap); Isaac Gym uses 100 m/s. Forum (unverified): raising above 10 after init had no effect.

## 5. Articulation contacts
- PhysX 5: "unstable robot simulations often feature a competing set of hard constraints such as contacts, very stiff joint drives"; fix by reducing drive stiffness / damping / max force or reducing the timestep "to ensure that the drive target is not achieved in a single solver step"; the constraint solved last effectively wins. https://docs.omniverse.nvidia.com/kit/docs/omni_physics/latest/dev_guide/guides/articulation_stability_guide.html
- Omniverse `solveArticulationContactLast` exists to "lead to a reduction in persistent contact depth" in gripping (not exposed in Unity): articulation-vs-object contacts staying penetrated under drives is a known effect.
- Mitigations (docs-backed): TGS + more position iterations, smaller dt, lower stiffness / forceLimit, primitive colliders, contactOffset just above rest.

## 6. Timestep
- Omniverse: avoid dt x natural frequency >> 1 (omega = sqrt(k / I)); Unity built-in PhysX has no substepping (lower fixedDeltaTime or Physics.Simulate). https://discussions.unity.com/t/physics-sub-stepping/821564

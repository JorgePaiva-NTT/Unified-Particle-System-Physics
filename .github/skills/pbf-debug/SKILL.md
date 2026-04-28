---
name: pbf-debug
description: 'Diagnose exploding, collapsing, jittering, or sticky Position-Based Fluid simulations in this Unity GPU particle project. Use when the fluid blows up on first frame, particles fly to infinity (NaN), the fluid pancakes / under-pressurizes, particles stick to the boundary, density never reaches rest density, or the simulation looked fine and then degraded after a kernel/buffer change. Walks through the canonical PBF failure modes: relaxation epsilon `e`, kernel radius vs grid cell size mismatch, missed double-buffer swap, phase-guard regressions, sub-MIN_ELEMENTS bitonic sort, and stale GridHash bounds. Targets Assets/Shader/Resources/FluidSolver.compute and Assets/Scripts/GPU/Fluids/FluidSolverN.cs.'
---

# PBF Debug — exploding / collapsing fluids

## When to use

Symptoms that should trigger this skill:

- Particles blow up on first frame (NaN positions, fluid disappears off-screen).
- Fluid "pancakes" or collapses flat — pressure never builds.
- Visible jitter / pulsing at rest.
- Particles glue to the boundary or punch through it.
- Worked before a recent edit to `FluidSolver.compute`, `FluidSolverN.cs`, `GridHash.cs`, or `BodyController.cs`.

Out of scope: cloth tearing, instancing/render bugs, scene-setup issues. For shader naming/binding rules see [shaders.instructions.md](../../instructions/shaders.instructions.md). For project-wide architecture see [AGENTS.md](../../../AGENTS.md).

## Mental model (read once)

PBF step in this codebase, per fluid body, repeated `DensityComputeIterations` times:

1. `PredictPositions` — gravity + velocity damping, write to `PredictedWRITE`. **Swap.**
2. `Hash.Process(PredictedREAD, Boundary)` — rebuilds `IndexMap` + `Table`.
3. `ConstrainPositions` loop (`ConstraintComputeIterations` × ):
   - `ComputeDensity` → fills `Densities`, `Pressures` (lambda).
   - `SolveConstraint` → applies position correction to `PredictedWRITE`. **Swap.**
   - `SolveClothCollision` → writes velocity into `VelocitiesWRITE`. **Swap predicted + velocities.**
4. `UpdateVelocities` — `(PredictedREAD - Positions) / dt`. **Swap velocities.**
5. `SolveViscosity` — XSPH average. **Swap velocities.**
6. `UpdatePositions` — copy `PredictedREAD` into `Positions`.

If any swap is missing, all later kernels read stale data → silent corruption.

## Procedure

Work the checklist top-to-bottom. **Stop at the first failure**, fix it, replay; do not batch fixes.

### 1. Confirm reproducibility

- Open [Assets/Test.unity](../../../Assets/Test.unity), play, observe.
- Note: first frame, after N seconds, only with body X, only after collision?
- If the failure depends on `NumParticles`, check the [BitonicSort floor](#5-bitonicsort-min_elements-floor) first.

### 2. Buffer-swap audit (most common regression)

Open [Assets/Scripts/GPU/Fluids/FluidSolverN.cs](../../../Assets/Scripts/GPU/Fluids/FluidSolverN.cs). For **every** kernel dispatch, verify:

- The kernel writes `*WRITE` buffers.
- A matching `STDUtils.Swap(controller.PredictedBuffer)` and/or `STDUtils.Swap(controller.VelocitiesBuffer)` follows the dispatch.

Canonical reference (do not edit unless intentional):

```csharp
fluidSolverShader.Dispatch(kernel, Groups, 1, 1);
STDUtils.Swap(controller.PredictedBuffer);
STDUtils.Swap(controller.VelocitiesBuffer);
```

Failure signature: NaN within ~5 frames, or rendering "frozen" particles while velocity grows. If a swap is missing, add it; do not "fix" by reordering kernels.

If you added a new kernel and the bug appeared, confirm:
- `*WRITE` slots are bound (not both `*READ`).
- Phase guard is present: `if (phase[id] != fluidId) return;`. A missing guard lets cloth particles get integrated as fluid → instant NaN.

### 3. Relaxation epsilon `e`

In [Assets/Shader/Resources/FluidSolver.compute](../../../Assets/Shader/Resources/FluidSolver.compute), inside `ComputeDensityAndPressure`:

```hlsl
float e = 150.0f;
pressure = -C / (sum_grad_C2 + e);
```

- **Symptom: collapsing / under-pressurized fluid** — `e` is too large; the constraint is over-relaxed. Try `50` → `30` → `15`. The fluid should regain volume.
- **Symptom: jitter or pulsing at rest** — `e` is too small; gradient denominator vanishes near smoothing-kernel boundaries. Increase by ~2×.
- **Rule of thumb:** `e` may need to *increase* as `NumParticles` grows (more neighbors → larger `sum_grad_C2`, but worst-case row near boundary is unchanged).

Change one value, replay. Do not also edit `KernelRadius` in the same iteration.

### 4. Kernel radius vs grid cell size

Two values must stay consistent:

- `Kernel.Radius` (CPU, [SmoothingKernel.cs](../../../Assets/Scripts/SmoothingKernel.cs)) is set from the constructor argument — `cellSize` in [FluidSolverN.cs](../../../Assets/Scripts/GPU/Fluids/FluidSolverN.cs):
  ```csharp
  var cellSize = controller.ParticleRadius * 4.0f;
  Hash = new GridHash(boundary.Bounds, total, cellSize, ...);
  Kernel = new SmoothingKernel(cellSize);
  ```
- The 27-cell `LOOP_START`/`LOOP_END` walk in `ComputeDensityAndPressure` only finds neighbors within **±1 grid cell**. Therefore the invariant is:

  `KernelRadius ≤ GridHash.CellSize`

If you tune `ParticleRadius` or change the `* 4.0f` multiplier without updating both call sites, neighbors fall outside the searched cells → density underestimates → fluid collapses.

To verify at runtime, log:
```csharp
Debug.Log($"KernelRadius={Kernel.Radius}, CellSize={Hash.CellSize}");
```
If `Kernel.Radius > Hash.CellSize`, raise the multiplier (e.g. `* 4.0f` is the floor for `Poly6`/`Spiky` support of `h = cellSize`).

If you change `cellSize`, you must `Dispose` and recreate the `GridHash` — see step 7.

### 5. BitonicSort `MIN_ELEMENTS` floor

[BitonicSort.cs](../../../Assets/Scripts/GPU/Sorting/BitonicSort.cs) requires `count ≥ 8192` (`BITONIC_BLOCK_SIZE * TRANSPOSE_BLOCK_SIZE = 512 * 16`). `GridHash.Process` sorts `IndexMap` whose count = `TotalParticles = controller.NumParticles + boundary.NumParticles`.

- Symptom: `ArgumentException: count < MIN_ELEMENTS` on first hash dispatch.
- Fix: raise `NumParticles` and/or boundary particle count; do not lower `MIN_ELEMENTS`.

### 6. Phase guard regression

Every per-particle kernel must start with:

```hlsl
if (phase[id] != fluidId) return;
```

Grep [FluidSolver.compute](../../../Assets/Shader/Resources/FluidSolver.compute) for `void ` to list kernels and confirm. A kernel processing all particles (cloth + fluid) with fluid math will produce explosions whose magnitude scales with cloth particle count.

Note: `phase` is `RWBuffer<int>` in shaders, **not** `StructuredBuffer<int>`. Don't change one side without the other.

### 7. Stale `GridHash` after parameter change

`GridHash` captures `Bounds`, `CellSize`, and `TotalParticles` in its constructor. Mid-run changes to particle counts, `ParticleRadius`, or `boundary.Bounds` are **not** picked up. `GridHash.Process(particles, boundary)` even has its size-mismatch guard commented out, so it will silently mis-bin.

If you changed any of those, verify [GPUParticleSystem.OnDestroy](../../../Assets/Scripts/GPU/GPUParticleSystem.cs) disposes the old `Solver` (which disposes its `GridHash`) and that you `StartFluid` again.

### 8. Ground plane / boundary sanity

`PredictPositions` clamps `predicted.y < _GroundLevel`. Confirm `_GroundLevel` is being set on the shader before dispatch (currently it isn't pushed every frame — if you rely on it, add `fluidSolverShader.SetFloat("_GroundLevel", GroundLevel)` next to the other `Set*` calls).

If the fluid sits on `y=0` instead of `GroundLevel`, this is the cause.

### 9. Last-resort isolation

If steps 1–8 didn't find it:

1. Set `ConstraintComputeIterations = 0` — disables `ComputeDensity` + `SolveConstraint`. If the explosion goes away, the bug is in the density/pressure path.
2. Set `DensityComputeIterations = 1` — minimizes substep coupling.
3. Comment out `SolveViscosity` dispatch. If jitter goes away, your XSPH viscosity coefficient is too high.
4. Reduce `NumParticles` to the `MIN_ELEMENTS` floor and dump `Positions`/`Densities` via `ComputeBuffer.GetData` to inspect a single frame.

## Quality criteria (you are done when)

- Density at rest hovers around the configured `Density` value (±10%).
- No NaNs after 30s of simulation.
- Particles do not penetrate the boundary by more than ~`ParticleRadius`.
- Toggling `synchronizeTime` does not change steady-state behavior qualitatively.
- The fix is a single-line / single-value change, or a single missing `Swap` — if you found yourself rewriting a kernel, you probably misdiagnosed.

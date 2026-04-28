# Unified Particle System Physics — Agent Guide

Unity 2022.3.62f3 project implementing a unified GPU particle solver (Position-Based Dynamics): fluids (PBF), cloth, and rigid-ish bodies share one particle pool and one neighbor grid. Main scene: [Assets/Test.unity](Assets/Test.unity).

> No build/test CLI. Open the project in Unity 2022.3.62f3 and play `Test.unity`. There is no test framework configured. Do not add one without asking.

## Two parallel implementations

[Assets/Scripts/](Assets/Scripts/) is split into two trees that mirror each other but are **not** kept in lock-step:

- [Assets/Scripts/CPU/](Assets/Scripts/CPU/) — reference / debugging implementations (`CPUParticleSystem`, `CPUCloth`, `CPUBody`...). Slower, single-threaded, easier to step through.
- [Assets/Scripts/GPU/](Assets/Scripts/GPU/) — production path. All real simulation runs here via compute shaders.

When fixing a bug or adding a feature, **default to changing the GPU path only** unless the user explicitly asks for CPU. Do not try to keep CPU and GPU in sync — the CPU code is intentionally a sketch.

## GPU architecture (the path that matters)

Entry point on the scene: [`GPUParticleSystem`](Assets/Scripts/GPU/GPUParticleSystem.cs) (MonoBehaviour). It owns:

- A single [`BodyController`](Assets/Scripts/GPU/BodyController.cs) — the shared particle pool. Holds `PositionsBuffer`, double-buffered `PredictedBuffer[2]` and `VelocitiesBuffer[2]`, `phaseBuffer` (per-particle int = which body/fluid/cloth it belongs to), `DensitiesBuffer`, `PressuresBuffer`.
- One or more [`Body`](Assets/Scripts/GPU/Body.cs) subclasses ([`FluidBody`](Assets/Scripts/GPU/Fluids/FluidBody.cs), [`ClothBody`](Assets/Scripts/GPU/Fluids/ClothBody.cs)) registered via `BodyController.AddBody`. Each `Body.CreateParticles` appends positions/velocities/colors/phase to the controller's lists; `BodyController.InitializeBuffers` uploads them.
- Solvers implementing [`ISolver`](Assets/Scripts/GPU/Fluids/ISolver.cs) / extending [`Solver`](Assets/Scripts/GPU/Fluids/Solver.cs): [`FluidSolverN`](Assets/Scripts/GPU/Fluids/FluidSolverN.cs) (PBF: predict → hash → density/pressure → solve constraints → update velocities → viscosity → update positions) and [`ClothSolver`](Assets/Scripts/GPU/Fluids/ClothSolver.cs) (distance constraints).
- A neighbor grid: [`GridHash`](Assets/Scripts/GPU/Grid/HashGrid.cs) (namespace `DefaultNamespace`) using [`BitonicSort`](Assets/Scripts/GPU/Sorting/BitonicSort.cs).
- A static boundary particle layer: [`FluidBoundary`](Assets/Scripts/GPU/Fluids/FluidBoundary.cs).

### Conventions you must follow

- **Double-buffer swap protocol.** `PredictedBuffer` and `VelocitiesBuffer` are `ComputeBuffer[2]` with `READ = 0`, `WRITE = 1`. After every kernel that writes `*WRITE`, call `STDUtils.Swap(controller.PredictedBuffer)` / `STDUtils.Swap(controller.VelocitiesBuffer)`. Forgetting a swap silently corrupts the simulation. See [`FluidSolverN.PredictPositions`](Assets/Scripts/GPU/Fluids/FluidSolverN.cs) for the canonical pattern.
- **Buffer naming on the shader side is fixed.** C# binds by string name: `Positions`, `PredictedREAD`, `PredictedWRITE`, `VelocitiesREAD`, `VelocitiesWRITE`, `Densities`, `Pressures`, `Boundary`, `IndexMap`, `Table`, `phase`, `clothData`. Renaming any of these requires updating *both* the `.compute` file and every `SetBuffer` call site.
- **Kernels are looked up by `FindKernel("Name")` every dispatch.** Cache the int if you add hot-path code, but match existing style otherwise (the existing solvers re-look-up each step). Kernel names currently in use: `PredictPositions`, `ComputeDensity`, `SolveConstraint`, `SolveClothCollision`, `UpdateVelocities`, `SolveViscosity`, `UpdatePositions` (fluid); `PredictPositions`, `SolveCollisions`, `UpdateVelocities`, `NodeUpdate`, `UpdatePositions`, `Setup` (cloth); `HashParticles`, `ClearTable`, `MapTable` (grid); `BitonicSort`, `MatrixTranspose`, `Fill`, `Copy` (sort).
- **Phase filter.** Every per-particle kernel begins with `if (phase[id] != fluidId/clothId) return;`. Preserve this when adding kernels — it is how the unified pool stays separated.
- **Thread group size = 128** for solver kernels (`#define THREADS 128` on both sides). `BitonicSort` uses `BITONIC_BLOCK_SIZE = 512` and requires `MIN_ELEMENTS = 8192` particles to run — fewer particles will throw.
- **Disposal.** Every `IDisposable` (`Solver`, `BodyController`, `GridHash`, `BitonicSort`, `FluidBoundary`) must be released in `GPUParticleSystem.OnDestroy`. Use `STDUtils.Release` helpers in [Assets/Scripts/CPU/STDUtils.cs](Assets/Scripts/CPU/STDUtils.cs) — they no-op on null and clear the reference.
- **Namespaces are inconsistent**: `GPU`, `GPU.Fluids`, `Assets.Scripts.GPU.Fluids`, `Assets.Scripts.GPU.Sorting`, `DefaultNamespace`. Don't "clean up" these without being asked; many files cross-reference all of them.

## Shader layout — [Assets/Shader/](Assets/Shader/)

- [`System.compute`](Assets/Shader/System.compute) — legacy DEM particle integrator (`CSMain`, `CSUpdateGrid`, `CSSolveCollisions`). Largely commented out from C#; treat as legacy unless the user references it.
- [`Resources/FluidSolver.compute`](Assets/Shader/Resources/FluidSolver.compute) — the active PBF solver. All fluid kernels live here.
- [`Resources/GridHash.compute`](Assets/Shader/Resources/GridHash.compute), [`Resources/BitonicSort.compute`](Assets/Shader/Resources/BitonicSort.compute), [`Resources/FluidBoundary.compute`](Assets/Shader/Resources/FluidBoundary.compute) — neighbor grid + sort + boundary sampling.
- [`Solvers/ClothSolver.compute`](Assets/Shader/Solvers/ClothSolver.compute) — cloth distance/bend constraints.
- Shared includes: [`Common.cginc`](Assets/Shader/Common.cginc), [`SystemDecl.cginc`](Assets/Shader/SystemDecl.cginc), [`Particle.cginc`](Assets/Shader/Particle.cginc), and [`Common/`](Assets/Shader/Common/) (`Defines.cginc`, `Kernels.cginc`, `Math.cginc`, `Random.cginc`).
- Surface/instancing shaders: [`InstaceSurfaceShader.shader`](Assets/Shader/InstaceSurfaceShader.shader) (note: filename is misspelled — preserved as-is, do not rename without grep-confirming no `.meta` GUIDs would break) and [`Debug.shader`](Assets/Shader/Debug.shader).

### Shader rules

- Includes use relative paths like `#include "./Common.cginc"` and `#include "./Common/Random.cginc"`. Keep paths relative; Unity will not resolve absolute Assets-paths reliably across platforms.
- Hash function (`HashIndex`/`HashScale`/`HashTranslate`/`HashSize`) is duplicated across `FluidSolver.compute`, `GridHash.compute`, and `ClothSolver.compute`. If you change it, change all three.
- Smoothing kernel coefficients (`Poly6`, `SpikyGrad`, `ViscLap`, `Poly6Zero`, `KernelRadius`, `KernelRadius2`) are computed CPU-side in [`SmoothingKernel.cs`](Assets/Scripts/SmoothingKernel.cs) and pushed via `SetFloat`. Do not hardcode them in shaders.
- The 27-cell neighborhood walk uses the `LOOP_START`/`LOOP_END` macros in `FluidSolver.compute`. Reuse them rather than rewriting nested loops.

## Common pitfalls

- Editing a `.compute` file does not always trigger a recompile — toggle play mode or right-click → Reimport if a kernel change appears to do nothing.
- `BitonicSort.Sort` requires power-of-two-friendly counts ≥ 8192. Reducing `NumParticles` below that will throw at runtime.
- `GridHash.Process(particles, boundary)` ignores its `numParticles + numBoundary != TotalParticles` check (commented out). If you change particle counts at runtime, recreate the `GridHash`.
- `phase` buffer typing is `RWBuffer<int>` in shaders but `ComputeBuffer(N, sizeof(int))` in C#. It is *not* a `StructuredBuffer`; do not change either side in isolation.
- `STDUtils.Swap` operates on `ComputeBuffer[]` references — `STDUtils.Swap(controller.PredictedBuffer)` swaps slots `[0]`/`[1]` in place. Don't replace with manual `ref` swaps; the array is shared with the C# layer.

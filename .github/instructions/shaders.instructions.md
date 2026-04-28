---
applyTo: "Assets/Shader/**"
description: "HLSL/ShaderLab conventions for the unified-particle compute shaders, includes, and kernel-binding contract with C#."
---

# Shader conventions (Assets/Shader)

These rules apply on top of [AGENTS.md](../../AGENTS.md). Read that first.

## Kernel-binding contract

Kernels are dispatched from C# via `ComputeShader.FindKernel("Name")` and `SetBuffer(kernel, "Name", buffer)`. **String names are the API.** When you add, rename, or remove a kernel or buffer in a `.compute` file, find every C# call site and update it in the same change — otherwise the dispatch silently no-ops or binds the wrong slot.

Authoritative C# binders to grep when changing names:
- [Assets/Scripts/GPU/Fluids/FluidSolverN.cs](../../Assets/Scripts/GPU/Fluids/FluidSolverN.cs)
- [Assets/Scripts/GPU/Fluids/ClothSolver.cs](../../Assets/Scripts/GPU/Fluids/ClothSolver.cs)
- [Assets/Scripts/GPU/Grid/HashGrid.cs](../../Assets/Scripts/GPU/Grid/HashGrid.cs)
- [Assets/Scripts/GPU/Sorting/BitonicSort.cs](../../Assets/Scripts/GPU/Sorting/BitonicSort.cs)

## Buffer naming (do not rename casually)

`Positions`, `PredictedREAD`, `PredictedWRITE`, `VelocitiesREAD`, `VelocitiesWRITE`, `Densities`, `Pressures`, `Boundary`, `IndexMap`, `Table`, `phase`, `clothData`, `Particles`.

Per-particle kernels start with the phase guard:
```hlsl
if (phase[id] != fluidId) return;   // or clothId
```
Preserve this on every new per-particle kernel — it is how a single particle pool is partitioned by body.

## Threading & sizing

- Solver kernels: `[numthreads(128, 1, 1)]`. `#define THREADS 128` on both sides.
- Grid kernels: `[numthreads(128, 1, 1)]`.
- Bitonic sort: `BITONIC_BLOCK_SIZE 512`, `TRANSPOSE_BLOCK_SIZE 16`. Particle count must be ≥ `8192` and ≤ `512*512`.
- Cloth `NodeUpdate` dispatches as 2D `(width/8, height/8, 1)` — keep cloth dims multiples of 8.

## Includes

- Use relative paths: `#include "./Common.cginc"`, `#include "./Common/Random.cginc"`. No absolute `Assets/...` paths.
- Shared math/constants live in [Common/](../../Assets/Shader/Common/). Prefer adding helpers there over duplicating across `.compute` files.
- The 27-neighbor cell walk is implemented as the `LOOP_START` / `LOOP_END` macros in [FluidSolver.compute](../../Assets/Shader/Resources/FluidSolver.compute). Reuse them.

## Smoothing kernel coefficients

`Poly6`, `Poly6Zero`, `SpikyGrad`, `ViscLap`, `KernelRadius`, `KernelRadius2` are computed CPU-side in [SmoothingKernel.cs](../../Assets/Scripts/SmoothingKernel.cs) and pushed each step. Do not hardcode coefficients in shader code.

## Hash function duplication

The `HashIndex`/`HashScale`/`HashTranslate`/`HashSize` block appears in `FluidSolver.compute`, `Resources/GridHash.compute`, and `Solvers/ClothSolver.compute`. If you change one, change all three or neighbors will mis-bin.

## Legacy code

[System.compute](../../Assets/Shader/System.compute) and [SystemDecl.cginc](../../Assets/Shader/SystemDecl.cginc) are the older DEM-style integrator and are mostly disconnected from the live solver path. Don't extend them unless the user explicitly asks; modify the PBF/cloth `.compute` files instead.

## Filename note

[InstaceSurfaceShader.shader](../../Assets/Shader/InstaceSurfaceShader.shader) is misspelled (`Instace` vs `Instance`). The `.meta` GUID is referenced from materials/scenes — do not rename without confirming no breakage.

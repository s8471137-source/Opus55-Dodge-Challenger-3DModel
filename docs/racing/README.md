# Local two-player crash racing (source implementation)

Date: 2026-10-26. Baseline: ef3bfec. Status: runtime C# compiled and pure rule tests passed; Unity scene generation, Editor compilation and gameplay verification are still pending.

## Features implemented in source

- Vertical split: player 1 left, player 2 right; two independent chase cameras, only one audio listener.
- Both Hellcats are added before Build() to ONE SoftBodyWorld. Both inputs are supplied before each batch of solver substeps. This is not two isolated simulations: the existing cross-body collision system handles their interaction, subject to runtime verification.
- SoftBodyCarView has an optional body override; existing single-player scenes use its original body by default.
- Each player chooses WASD keyboard layout, arrow keyboard layout, or a specific currently connected gamepad. Two keyboard layouts may share one keyboard; identical layouts or the same pad cannot be assigned twice. Pad disconnection / index reassignment pauses racing until devices are chosen again.
- Start countdown, three laps, sixteen ordered checkpoint gates, forward-only crossing, individual speed/lap HUDs, shared simulation-time clock, first finisher, pause, restart, return to sandbox in the combined build.
- Per-player repair at the last passed checkpoint: R for WASD, Num0 for arrows, Back/Select for a gamepad. Checkpoint progress is retained, no extra lap awarded. Repair is intentionally free in this first local prototype.
- RaceSceneBuilder creates CrashCircuit: an approximately 810 m oval circuit, 22 m asphalt ribbon, red/white barriers, two half-width jump ramps with bypass lanes, five alternating slalom barriers, visible checkpoint arches and start/finish marking. Track statics use the custom solver's geometry sources, not PhysX vehicle collision.
- Sandbox start-screen race button / F2 and pause-menu race button are displayed when CrashCircuit is present in the build. Windows and macOS sandbox build tasks now generate and include that scene.
- Bundled Liberation Sans font with its license replaces reliance on OS LegacyRuntime for start, pause and race UI. This is an attempted source-level fix for the earlier Wine font failure, not a verified runtime fix.

## Controls

| Device | Throttle/brake/reverse | Steering | Handbrake | Repair |
|---|---|---|---|---|
| Keyboard WASD | W / S, hold S at rest for reverse | A / D | Space | R |
| Keyboard arrows | Up / Down, hold Down at rest for reverse | Left / Right | Right Ctrl | Num0 |
| Assigned gamepad | RT / LT, hold LT at rest for reverse | Left stick | B / East | Back / Select |

Transmission is automatic; ABS and traction control are enabled. Esc opens the shared pause/device menu. Keyboard ghosting depends on the physical keyboard; gamepads avoid that limitation.

## Generate and play in Unity

1. Open `Unity/HellcatSandbox` in Unity **6000.6.0f1** with the pinned packages.
2. If the imported Hellcat prefab is missing, use the existing car importer first.
3. Run **Tools > Hellcat > Build Two Player Race Scene**.
4. This creates and saves `Assets/Scenes/CrashCircuit.unity`, persistent ramp meshes/materials, and adds the race scene to Editor build settings.
5. Press Play, assign two devices/layouts, then START RACE.

No hand-written scene YAML is supplied. The scene and generated assets must be produced by the editor builder; that builder has not been executed in this sandbox.

### Standalone race build

Use the existing Unity batch wrapper with:

```powershell
& tools\unity\run_unity_batch.ps1 -LogFile docs\racing\unity-build.log -ProjectPath Unity\HellcatSandbox -UnityArgs @('-executeMethod','Hellcat.EditorTools.Build.RaceSceneBuilder.BuildPlayer')
```

Output: `Builds/CrashCircuit/CrashCircuit.exe`.
For sandbox plus race use the existing `Hellcat.EditorTools.Build.BuildTasks.BuildSandbox` entry point; it now includes TestMap and CrashCircuit. The old downloaded release does NOT contain these source changes. Do not substitute the separately compiled check DLL into it: its serialized scenes have not been rebuilt for this mode.

## Checks actually run

- Roslyn from .NET SDK 8.0.427 compiled ALL runtime `Assets/Game/**/*.cs` files against the Unity/Sim/input/Burst libraries extracted from release v1.0. Result: **0 errors, 42 warnings** in existing code. This is an API/compilation check, NOT Unity's import, asset serialization, Burst compilation or player build.
- `tools/racing/RaceRulesTests.csproj` executed on .NET 8: **18 checks passed**. Tests exercise the same RaceRules class the runtime uses: forward/backward crossings, width/height limits, duplicate/missing devices, all requested input combinations, and three ordered laps.
- Fresh logs: `docs/racing/runtime-compile.log`, `docs/racing/rules-tests.txt`.

Re-run pure tests:

```bash
dotnet run --project tools/racing/RaceRulesTests.csproj
```

## Still required before calling this playable/verified

- Unity Editor import and compilation of the editor builder.
- Generate CrashCircuit and visually inspect road/barrier joints, ramps, spawn heights and gate clearances.
- Build and launch the new player; verify the bundled font actually renders on Windows and Wine.
- Real two-person tests for WASD + arrows, keyboard + pad in both player orders, and two independent pads. Verify hot-unplug/reassignment and keyboard ghosting behavior.
- Car-to-car collision, detached parts, two-view skinning and repair regression tests.
- Check countdown input lock, reverse, wrong-way gates, missed checkpoints, repair without skipped progress, three-lap finish, pause and scene transitions.
- Measure physics speed and render FPS: two soft-body cars and two views may be substantially slower than the historical single-player measurements. No two-player performance claim is made.
- Re-run existing single-player validation/build gates after integration.

Unity Editor is not installed/licensed here. A new executable or screenshot of the new mode has NOT been produced. The earlier Wine screenshot is of the unmodified release, not this implementation.

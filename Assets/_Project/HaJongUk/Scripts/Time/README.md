# 時間 / Time system

`HJU_TimeMovement_Integration.unity` combines the camera/movement work scene with
the time test system. It reuses the player camera, work-scene sun and navigation;
the original two scenes remain available. The movement panel is on the left and
the time/weather panel on the right. Press Esc to release the cursor, operate
either panel, then click the world to resume camera movement. A scene-only input
bridge holds the player's input lock while the pointer is over the time panel.
Camera-direction buttons are hidden in this scene so the player rig remains the
sole camera rotation owner. The time test still uses a 60-second day for checking.
Both panels share the same reversible pointer-lock helper. `BusinessTimeController`
owns demo-clock progression after it is configured by the test panel, so hiding the
panel does not stop time. Standalone/external production clock ownership is unchanged.

TimeSystem_Test.unity: 24 game hours in 60 real seconds for quick checks. Production target remains 3–5 minutes; TimeDemoPanel.fullDayDurationSeconds controls the demo.

Both TimeSystem_Test and Team_Integration start at 08:00 and advance through midnight to 08:00 the next morning (openingHour=8, closingHour=32). Team_Integration currently uses a 120-second standalone preview cycle; choose 180–300 seconds for production. Midnight wraps only the displayed hour, so cloud drift stays continuous.

SkyOverlay renders the derivative-antialiased moon disk and halo, dense stars, and clouds that remain visible at night. One CelestialMath SunSample drives sky/cloud, rain tint and ambient transitions together. The unused hidden moon mesh/material is no longer created. The cloud shader uses 24 density samples per pixel; target-device GPU performance has not been measured.

For the presentation, set `DayNightPresenter` → `일몰 속도 배율` to `0.25`. The setting ranges from `0.1` to `2`, with `1` reproducing the original motion. It controls the sun and lighting transition near the western horizon. Set the UI's day duration separately to `105` seconds for a 1m 45s play window. The sunrise and sunset clock readings and the moon path keep their configured values.

Unity Procedural Skybox provides the single sun disk. SkyOverlay provides animated clouds, night stars, the luminous moon disk and halo. CelestialBodyPresenter owns lunar art settings and weak moonlight. No external sky textures are required. Runtime materials and objects are released on disable.

External time mode uses BeginDay and ApplyExternalProgress. Connect ClosingRequested to customer cleanup and UI settlement, then ConfirmSettlement before the next day. Actual UI settlement/cursor integration remains pending.

Verified via MCP Play Mode: daylight clouds, 22:00 moon and stars, moon halo. Rendering is an artistic approximation, not astronomical accuracy. No scene loading buttons are provided.

## Rain weather

`RainWeatherController.isRain` is the target, not an instantaneous emission switch.
It starts dry even when checked on entry. The pure `RainTransition` envelope rises
over 12 scaled real seconds and falls over 10; Inspector fields tune both durations.
During onset the first half forms the rain clouds, then the second half gradually
increases rainfall. Clearing reverses this sequence: rain stops before clouds clear.
Smoothstep eases the endpoints. Reversing the toggle continues from the current
envelope without resetting. `Intensity` (0–1) drives emission; `CloudIntensity`
drives `_Rain` on the existing sky material: cloud bodies broaden/merge, their coverage increases and
their color darkens, while night clouds remain dark. Rain also adds distance fog;
the environment's Rain Fog Percent controls its maximum strength from 0 to 100%.
Fog follows the actual rain intensity, fades with rain and uses a darker night color.
The active scene's previous fog settings are restored when the effect ends or the
environment is disabled. Fog applies to compatible scene materials, not the skybox.

Owner: HaJongUk's scene-local controller, one weather envelope advanced by the
environment composition in main-thread Update. TimeSkyExposure receives an
immutable SkyFrame after weather advances. This is visual weather, independent of
day duration and not a source of settlement/gameplay effects. BusinessClock.IsPaused
and Time.timeScale stop the envelope and native particle playback together.
Closing leaves weather running so the test toggle still works after the 60s demo.
Disabling destroys owned particles/materials and resets dry; enabling fades in again.
No global singleton, background work, imported texture or package is required.

The test panel exposes the live isRain checkbox, rain fog strength slider, intensity percentage and particle
count. Scene builder wires the references; it adds no scene-navigation buttons.
The default camera-follow emitter covers 30×30m from 12m above the camera. For a
defined map, select `고정 영역 사용`, set `고정 영역 중심` to the emitter's
world-space center and height, and set `비 영역 크기` to its X/Z width and depth.
Selecting the controller shows the fixed area in Scene view. `최대 빗방울 낙하 거리`
must reach the lowest ground below the emitter. Fixed-area drops fall vertically;
camera-follow drops retain their small wind. Existing drops remain in world space
after moving/changing the area, and splashes can extend slightly past its edge.
`초당 최대 빗방울 수` and `빗방울 생성 상한` independently limit the emission rate;
their effective maximum is the lower of the two (1600/s budget by default, including
older scenes serialized with 2200). Increasing area without increasing the rate makes
rain sparser; raising the budget can cost more and requires target-device profiling.
The live drop cap scales with budget and fall distance, up to 16,000; collision uses
static world colliders.
Residual drops fall out after emission reaches
zero instead of disappearing suddenly. Roofs/ground require colliders. This local
camera rain effect does not simulate water, puddles or whole-map weather physics.
Cloud marching retains the existing 24-step budget; no target GPU timing claimed.

Validation: RainTransitionTests covers onset, clearing, reversal, pause, frame-step
independence, endpoints and invalid numeric inputs. Play checks should cover clear,
light/heavy rain, rapid reversal, day/night, pause/resume, teardown and repeat play.
Native `Play` schedules particle simulation; gameplay pause sets both systems'
simulationSpeed to zero. No per-frame `Simulate`, particle-array readback,
OnParticleCollision callback, Instantiate or Destroy is used during rainfall.
Collision uses Low quality's static voxel cache (1m voxels, 128 collision shapes),
with dynamic colliders disabled. Configure `rainCollisionLayers` for ground/roofs;
moving roofs require a different collision policy. This is local visual rain.

Collision sub-emission creates two short-lived upward spray droplets at 22% of
impacts, with gravity and an alpha fade. The one reused splash system has a 768
particle cap; it emits no continuous rain and no physics/collision callbacks.
All owned systems/materials are released on disable. `VisibleSplashCount` is shown
on the test UI. UI labels refresh at 10Hz instead of allocating formatted strings
on every IMGUI layout/repaint pass. The cloud/sky appearance and envelope are unchanged.

## Environment composition (2026-11-17 refactor)

Assign only Sun Light and View Camera on `TimeEnvironmentController`. It composes
the required local clock/presenters once, then explicitly calls solar sampling,
lunar sampling and weather advance in Update (order 100), before Unity's native
particle phase. Sky rendering consumes that frame in LateUpdate after camera movement.
There is no global context, component registry or service lookup available to
consumers. No presentation component subscribes to the clock or runs its own
Update/LateUpdate. BusinessTimeController remains the sole business clock owner.

`CelestialMath` is pure C# in Core, with finite/range validation and regression
tests for the existing artistic paths. `DayNightPresenter` owns sun/ambient art
settings and restoration; `CelestialBodyPresenter` owns lunar settings/moonlight;
`TimeSkyExposure` owns sky resources and accepts only SkyFrame values and camera
position/far distance. `RainWeatherController` owns the weather envelope and
`RainParticleEffect` exclusively owns the two particle systems/materials. Demo UI
reads narrow environment state and commands plus the local clock, without camera
or weather component assignments. Disable the environment to release its effects;
re-enable to reconstruct them once. Changing the isRain target never resets it.

Existing script GUIDs and art/weather setting names are retained. The explicit
Editor `TimeEnvironmentMigration.Migrate()` binds and saves only the two owned
scenes, preserves scene copies under output/refactor-backup, and can be repeated
without adding duplicate environment components. Scene/reference verification and
Play checks are required after migrating; do not hand-rewrite scene fileIDs.

Refactor verification: serialized component assignment fields decreased from
12 to 2 in TimeSystem_Test and 9 to 2 in Team_Integration. All 22 EditMode
calculation/weather tests passed. Team Play checks passed pause, draining,
weather cleanup/re-enable and full environment cleanup/recreation without duplicate
effects. Samples at 08:00, 12:00, 18:00, 19:00, 22:00 and next-day 05:30/06:30
matched the requested game hours; noon/night screenshots retain one sun, moon,
stars and dark night clouds. Restored the saved test scene in Edit Mode with one
environment, two bindings, zero runtime particle systems and no Console errors.
Player build and target-device profiling were not performed for this refactor.

Native API reference: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ParticleSystem.SubEmittersModule.AddSubEmitter.html

`RainWeatherVerification.RunLive("output/rain-after.txt")` explicitly verifies the
open time test scene through the Editor. It uses runtime-only transition overrides,
samples frames after a 3s warmup, captures the ground, checks pause, draining,
disable cleanup and re-enable. It never saves scenes or closes an interactive
Editor. Compare the same Editor/GPU/view conditions; this is not Player profiling.

2026-11-16 validation (Unity 6000.6.4f1 / URP 17.6, local MCP): all 6 EditMode
tests passed. Test-map Play Mode sampled 0%, 7.45% (2s), 50.06% (6s), 100%
(12.5s); clearing reached zero and remaining drops drained to zero. Shader `_Rain`
matched the envelope. Night clouds, frozen particles during pause, reversal without
a jump, and a second Play entry with one emitter were checked. Both shaders compiled
without errors; runtime Console was empty. Team_Integration is also wired and reached
100% rain / 3,520 particles with matching cloud intensity. Both scenes keep isRain
false by default and fog stayed false. The original test scene is restored after checks.
Player builds, target-device performance and roof collision coverage across the entire
map remain unverified.

2026-11-17 verification (Unity 6000.6.4f1 / URP PC_RPAsset, GTX 1060 / D3D11):
quiet Editor Play Mode samples after a 3s warmup covered 15s per scene. Time test:
2,288 samples, p95 8.34ms, max 24.94ms, no frames above 33.33ms; rain Update
max 0.0713ms and synchronous ParticleSystem.Simulate 0ms. Team_Integration after
the final Play(false) change: 2,223 samples, p95 8.54ms, max 15.88ms, no frames
above 33.33ms, rain Update max 0.0386ms. Live splash peaks were 183/114.
Both checks passed pause stability, natural draining, cleanup and re-enable.
The test scene was restored with isRain=false, no leftover particle systems,
no shader errors and no runtime Console errors. These are Editor callback frame
intervals with profiler markers, not Player/GPU frame-time guarantees. Initial
before/after trials included different Editor activity and are not used to claim
a percentage speedup. Reports/screenshots are under local output/rain-*.txt/png.

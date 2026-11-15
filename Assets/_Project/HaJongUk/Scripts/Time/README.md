# 時間 / Time system

TimeSystem_Test.unity: 24 game hours in 60 real seconds for quick checks. Production target remains 3–5 minutes; TimeDemoPanel.fullDayDurationSeconds controls the demo.

Unity Procedural Skybox provides the single sun disk. SkyOverlay provides animated procedural clouds and night stars. CelestialBodyPresenter provides a luminous moon disk, halo direction and weak moonlight. No external sky textures are required. Runtime materials and objects are released on disable.

External time mode uses BeginDay and ApplyExternalProgress. Connect ClosingRequested to customer cleanup and UI settlement, then ConfirmSettlement before the next day. Actual UI settlement/cursor integration remains pending.

Verified via MCP Play Mode: daylight clouds, 22:00 moon and stars, moon halo. Rendering is an artistic approximation, not astronomical accuracy. No scene loading buttons are provided.

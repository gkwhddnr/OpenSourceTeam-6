# 時間 / Time system

TimeSystem_Test.unity: 24 game hours in 60 real seconds for quick checks. Production target remains 3–5 minutes; TimeDemoPanel.fullDayDurationSeconds controls the demo.

Both TimeSystem_Test and Team_Integration start at 08:00 and advance through midnight to 08:00 the next morning (openingHour=8, closingHour=32). Team_Integration currently uses a 120-second standalone preview cycle; choose 180–300 seconds for production. Midnight wraps only the displayed hour, so cloud drift stays continuous.

SkyOverlay renders the derivative-antialiased moon disk and halo, dense stars, and clouds that remain visible at night. DayNightPresenter.NightBlend drives sky/cloud and ambient transitions together. The previous moon mesh renderer is disabled to avoid a duplicate disk. The cloud shader uses 24 density samples per pixel; target-device GPU performance has not been measured.

Unity Procedural Skybox provides the single sun disk. SkyOverlay provides animated procedural clouds and night stars. CelestialBodyPresenter provides a luminous moon disk, halo direction and weak moonlight. No external sky textures are required. Runtime materials and objects are released on disable.

External time mode uses BeginDay and ApplyExternalProgress. Connect ClosingRequested to customer cleanup and UI settlement, then ConfirmSettlement before the next day. Actual UI settlement/cursor integration remains pending.

Verified via MCP Play Mode: daylight clouds, 22:00 moon and stars, moon halo. Rendering is an artistic approximation, not astronomical accuracy. No scene loading buttons are provided.

# Earth, Seasons, and Real-Time Sun/Moon Position

Reviewed: 2026-10-11. These are astronomical principles and implementation
guidelines, not an installed simulator or a guarantee of observational accuracy.
Select the actual engine/package version before using engine APIs.

## 1. Rotation, Revolution, and the Equator

Earth rotates eastward: this produces the daily apparent movement of celestial
bodies through a local sky. Rotation relative to stars is about 23 h 56 min;
the mean solar day is about 24 h because Earth also advances around the Sun.
A season-tracking tropical year is approximately 365.2422 days; a Gregorian
calendar handles this through its leap-year rules, not 12 equal-length months.

The equator is the plane perpendicular to Earth's rotation axis; latitude
measures location north/south of it. Earth's axis is tilted about 23.44 degrees
relative to the normal to its orbital plane. The changing orientation of the
hemispheres toward sunlight changes noon elevation and daylight duration.
Orbital distance alone does not explain opposite seasons in the two hemispheres.

Sources: [NASA seasons](https://spaceplace.nasa.gov/seasons/en/),
[USNO sidereal time](https://aa.usno.navy.mil/faq/GAST).

For an educational globe, model axial rotation and orbital motion separately,
preserving the tilted axis orientation over one year. For an ordinary local
game scene, calculate observer-relative light directions; physically moving
the whole level or globe is unnecessary. A geometric season does not prescribe
local temperature, rain, snow, vegetation, or weather: those are separate systems.

## 2. January to December

This table uses meteorological seasons in temperate regions and qualitative
solar-declination trends. Equinoxes/solstices occur within the indicated months;
compute their date/time for the required year rather than hardcoding a day.

| Month | Northern hemisphere | Southern hemisphere | Solar declination trend |
|---|---|---|---|
| 1 | Winter | Summer | Negative, moves north after December solstice |
| 2 | Winter | Summer | Negative, moves toward zero |
| 3 | Spring | Autumn | Crosses zero northward at March equinox |
| 4 | Spring | Autumn | Positive, moves north |
| 5 | Spring | Autumn | Positive, approaches northern maximum |
| 6 | Summer | Winter | Near +23.44 degrees at June solstice, then turns south |
| 7 | Summer | Winter | Positive, decreases |
| 8 | Summer | Winter | Positive, moves toward zero |
| 9 | Autumn | Spring | Crosses zero southward at September equinox |
| 10 | Autumn | Spring | Negative, moves south |
| 11 | Autumn | Spring | Negative, approaches southern maximum |
| 12 | Winter | Summer | Near -23.44 degrees at December solstice, then turns north |

At the equator, daylight stays close to 12 hours and the noon Sun switches
between the north and south side of the sky. Tropical wet/dry seasons are not
a universal four-season mapping. Higher latitudes have greater seasonal daylight
variation, including polar day/night. Equinox day/night are not exactly equal
when refraction and the visible disk are included.

The month table is an explanatory calendar convention; calculate continuous
positions from the full timestamp. Do not snap light rotation at month boundaries.

## 3. Observer and Clock Contract

Record these inputs explicitly:

- UTC date/time, including year, leap days, and supported date range.
- Geodetic latitude phi: north positive, [-90, +90] degrees.
- Longitude lambda: east positive, with one normalized wrap convention.
- Observer height and Earth ellipsoid when topocentric precision matters.
- World true-north orientation and the selected local/world coordinate basis.
- Accuracy mode, geometric/apparent position, refraction policy, and data version.

Latitude or equator alone cannot determine a body's current sky direction.
Longitude and time determine the observer's meridian; height and parallax affect
an observer-relative solution. Convert civil time/time-zone/DST to UTC once.
Do not add a time-zone correction again to an already-UTC timestamp.
A UTC offset is not a longitude, and magnetic north is not true north.

Use an explicit simulation clock: real wall-clock UTC or an epoch plus scaled,
pausable elapsed time. Store a double-precision epoch/time representation and
derive each result from it; avoid accumulating float Euler-angle increments.
Julian Date is a continuous day count with a noon boundary, not day-of-year.
For high accuracy, distinguish UTC, Earth-rotation UT1, and ephemeris TT/TDB;
use the library's required conversions and Earth-orientation/leap-second data.

## 4. Solar Position: Orbit to the Local Horizon

Choose one documented route; do not combine unrelated time corrections.

**Solar approximation route:** compute solar declination delta and equation of
time E from the timestamp using a documented solar algorithm. With UTC minutes
m, longitude in degrees east, and E in minutes:

```text
trueSolarMinutes = wrap(m + E + 4 * lambda, 1440)
H_degrees = trueSolarMinutes / 4 - 180
```

Here H is local solar hour angle, negative before solar noon and positive after.
Use calendar/day-of-year and leap-year handling from the selected algorithm.
See the [NOAA equations](https://gml.noaa.gov/grad/solcalc/solareqns.PDF).
NOAA's [calculator notice](https://www.gml.noaa.gov/grad/solcalc/calcdetails.html)
says it is no longer maintained; treat the published equations as a documented
approximation, not a maintained runtime service or an accuracy promise.

**Ephemeris route:** obtain a Sun vector or right ascension alpha and declination
delta, consistent with the chosen epoch/frame. Earth's heliocentric position
determines the geocentric Sun direction. An educational elliptical orbit may use
Kepler's equation M = E_anomaly - e*sin(E_anomaly), then orbital-plane position
a*(cos(E_anomaly)-e, sqrt(1-e*e)*sin(E_anomaly), 0), rotated by its orbital
orientation. This idealized model omits perturbations and apparent corrections;
use validated ephemerides for observational accuracy.

Convert compatible equatorial coordinates using local sidereal time:

```text
LST_degrees = wrap(GST_degrees + lambda, 360)
H = wrapSigned(LST_degrees - alpha_degrees)
```

Convert RA/sidereal hours to degrees by multiplying by 15 when needed.
Mean coordinates require a compatible mean sidereal frame; apparent coordinates
require a compatible apparent frame. Do not use J2000 RA with an unrelated
equinox-of-date sidereal angle without the required transformation.

For a simple horizon transform, convert phi, delta, and H to radians:

```text
east  = -cos(delta) * sin(H)
north =  cos(phi)*sin(delta) - sin(phi)*cos(delta)*cos(H)
up    =  sin(phi)*sin(delta) + cos(phi)*cos(delta)*cos(H)
altitude = asin(clamp(up, -1, +1))
azimuth  = wrap(atan2(east, north), 2*pi)
```

This ENU vector is the local observer-to-body direction. Azimuth is measured
clockwise from true north: north=0, east=90, south=180, west=270 degrees.
At the zenith/nadir, azimuth is undefined; retain the direction vector and avoid
unstable Euler-angle interpolation. This transform does not add parallax or
atmospheric refraction by itself.

Reference: [USNO horizon conversion](https://aa.usno.navy.mil/faq/alt_az).

## 5. Moon Position and Phase

The Moon's orbital period relative to stars is about 27.3217 days; its phase
cycle relative to the Sun is about 29.53 days. Its orbital plane is inclined
about 5.145 degrees to the ecliptic. Changing orbital orientation and perturbations
make a fixed circular orbit or a fixed offset from the Sun inadequate for accuracy.

Source: [NASA Moon fact sheet](https://nssdc.gsfc.nasa.gov/planetary/factsheet/moonfact.html).

Obtain independent lunar position/distance from a validated lunar ephemeris
or a documented approximate model with an explicit epoch, orbital elements,
valid date range, and measured error. Rotate ecliptic coordinates into the
chosen equatorial frame before applying the horizon transform. Do not set
MoonDirection = -SunDirection: the Moon can appear in daytime and is not always
opposite the Sun.

The nearby Moon particularly needs topocentric correction. In a single common
Earth-fixed frame, subtract the observer's ellipsoid-based position from the
body's geocentric position, normalize that vector, and project it onto the
observer's east/north/up basis. Do not subtract vectors in different frames,
units, or epochs. A library may perform this correction directly; do not apply
it twice. Geocentric RA/declination plus the simple horizon formula alone
does not supply this correction.

[NASA/JPL SPICE observer-relative states](https://naif.jpl.nasa.gov/pub/naif/toolkit_docs/C/cspice/spkcpo_c.html)
supports observer position/frame and correction choices. If selected, load
compatible time, planetary, and Earth-orientation/frame kernels with coverage
for the requested dates; this guide does not mandate installing SPICE.

For phase illumination, let i be the angle between Moon-to-Sun and
Moon-to-observer vectors. The ideal illuminated disk fraction is
k = (1 + cos(i)) / 2. Use directions at consistent times/frames and distinguish
illuminated fraction from waxing/waning and from the disk's visible orientation.
Position, disk phase, brightness, angular diameter, and eclipse shadowing are
separate outputs; an ordinary directional light does not solve them all.

## 6. Horizon, Refraction, and Visibility

Specify geometric vs refracted altitude and center vs upper-limb events.
Refraction depends on atmospheric conditions and is uncertain near the horizon.
A common solar rise/set approximation uses center altitude about -0.833 degrees;
this is not a universal Moon threshold or a correction to every sky sample.
Observer elevation, local terrain, clouds, and horizon obstruction are separate.
An altitude below zero does not eliminate twilight or residual ambient light.

A rise/set search can produce no event during polar day/night. Return a clear
no-event state; do not invent a time by forcibly clamping an out-of-range
physical solution. Clamp inverse-trig inputs only for floating-point roundoff.

## 7. Real-Time Scheduling and Verification

- Keep the astronomical solver pure: input snapshot -> positions/phase ->
  engine adapter. Validate data and references before updating scene objects.
- Recompute when time/location/model inputs change. Use a bounded timer cadence
  selected from time scale and allowed angular error; there is no universal
  one-second interval suitable for every simulation speed.
- Interpolate normalized directions or quaternions for smooth presentation;
  do not lerp wrapped azimuth from 359 to 1 through 180 degrees.
- Rebuild immediately after time jumps, location changes, or time-scale changes.
  Cache ephemeris data and avoid per-frame downloads, allocations, scene-wide
  searches, or expensive environment/probe recaptures.
- For networking/replay, use authoritative epoch, rate, and pause state; do not
  let client machine time independently determine gameplay-authoritative daylight.
- Check cardinal directions, hemispheres, equator, high latitudes, leap day,
  UTC midnight/year rollover, east/west longitude, and time-zone conversion.
- Verify solar noon with the computed hour angle, not assumed civil 12:00.
  Test Sun/Moon independently, including a daytime Moon and near-horizon parallax.
- Compare selected timestamps/locations against independent official almanac
  output, with identical geometric/apparent conventions and documented tolerance.
  Record expected/actual angles, units, model/date range, and remaining errors.
- Test engine light direction, disk position, shadows, exposure, fog/cloud
  response, ambient/reflection refresh, save/reopen, and accelerated-time cost.
  A document check does not prove an implemented celestial system works.

## 8. Unity Integration

Use a pure C# solver (double precision) and a separate main-thread scene adapter.
C# / Visual Scripting should consume the same result; avoid duplicate clocks.
Jobs/Burst/native astronomy plugins require actual platform/package compatibility
and measurements; do not introduce them merely for this documentation example.

An example local basis is +X east, +Y up, +Z true north. It gives
D_world = (east, up, north). Apply the configured north/origin rotation once;
this is a declared scene convention, not automatic georeferencing.
A visible Sun/Moon disk lies toward D_world, while directional light rays travel
toward -D_world. For a suitable Unity Directional Light, construct rotation with
`Quaternion.LookRotation(-D_world, safeUp)`; validate shadows and use a stable
fallback up vector near collinearity. Do not mix this with an unconverted
Unreal Euler rotation.

Reference: [Quaternion.LookRotation](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Quaternion.LookRotation.html).

Set the configured sun reference and sky shader inputs for Built-in/URP;
verify moon-disk and multi-light support in the actual renderer. In HDRP, drive
appropriate Directional Lights and Physically Based Sky celestial-body settings.
Affect Physically Based Sky / celestial rendering settings depend on the package;
these render a body from its direction, not compute an Earth ephemeris.

Reference: [Unity HDRP light documentation source](https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.render-pipelines.high-definition/Documentation~/reference-light-component.md).
This source tracks development; select the installed package's corresponding
documentation before implementation.

Preserve shared Volume Profiles/sky materials and the existing time-of-day owner.
Rate-limit ambient/probe/sky environment updates independently from visible light
rotation. Handle destroyed Unity objects, cancelled async work, and scene unload.
Do not rewrite Terrain, vegetation, or weather assets simply because the month
changes; connect optional seasonal presentation through explicit project settings.

## 9. Numerical Contract Review (2026-11-11)

These are implementation acceptance criteria; they do not certify a particular
astronomical library or revise the original source-review date.

- Reject non-finite timestamps/angles/vectors and out-of-range latitude. Normalize
  longitude with an explicit wrap rule; do not clamp a bad latitude into validity.
- Record units and frame/epoch at each boundary. Convert degrees/radians and
  RA hours once; retain double precision until the rendering boundary.
- Associate each result with input timestamp, location, algorithm/data version,
  validity, and precision mode. Discard obsolete async results after a time jump,
  location change, scene unload, or newer request.
- Test horizon-vector length and east/west signs independently of rendered light
  orientation. At zenith/nadir, preserve the vector instead of asserting a unique
  azimuth. Treat unavailable rise/set events separately from numerical failures.
- Choose explicit handling for dates outside ephemeris/kernel coverage; do not
  silently extrapolate while claiming the original accuracy.
- Keep fast educational approximation and observer-corrected ephemeris modes
  visibly distinct in configuration and verification records. Runtime code,
  astronomical-reference accuracy, and engine rendering require separate evidence.

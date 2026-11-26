# Junie Entry Instructions for Unity

Before substantive work:

1. Read the project-root `AGENTS.md` completely.
2. Read the project-root `PROJECT_CONVENTION.md` completely.
3. Read any nearer `AGENTS.md` applicable to the affected package or feature.
4. Inspect the Unity version, packages, asmdefs, relevant scripts/tests, affected
   assets and settings, Editor state, and repository status.

Do not treat `TBD` values as facts. Preserve asset `.meta` files and GUIDs. Use
Unity-aware tools for scene, prefab, and reference-sensitive asset changes. Do not
edit Unity-generated solution/project files, caches, package-cache content, or
user-local settings.

Apply the root rules for managed/native/GPU allocation and release, forced-GC/unload
boundaries, reference-safe file cleanup, and versioned Asset Store/UPM/vendor asset
updates. Preserve source/license records, local overrides, compatibility, and rollback.

Keep changes focused and preserve unrelated or unsaved work. Ask before destructive
asset operations, broad import/reserialization, Editor restart, package installation,
external or credentialed actions, deployment, or publication. Report only Unity
compilation, tests, builds, and Editor actions whose results were observed.

`.aiignore` protects JetBrains AI Assistant and Junie but is not guaranteed to
restrict every third-party agent. Do not transmit private code or assets to an
unapproved destination.

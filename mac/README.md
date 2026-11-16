# Native implementation

`build.sh` compiles `Sources/*.swift` and `App/main.swift` with the installed
Swift compiler, builds the ICNS from the repository PNG with in-box Apple tools,
and ad hoc signs `dist/AutoWrite.app`. No package manager or dependency is used.
The stable bundle identifier is `org.autowrite.AutoWrite`. The deployment target
is macOS 14 on arm64. Swift 5 language compatibility mode keeps AppKit/Carbon's
callback interfaces usable with the installed Swift 6 toolchain.

`test.sh` compiles the same production sources with `Tests/*.swift`. The core
uses only Foundation; the native runtime and windows add Apple's AppKit,
ApplicationServices, Carbon, and Darwin. The test entry point is separate from
the application's entry point. Running tests never runs the live probe.

- `Compatibility.swift`: seeded .NET Framework Random, UTF-16 character tests,
  integer parsing, even rounding and Framework fixed-decimal formatting.
- `Settings.swift`: explicit stored order, labels, bounds, defaults and INI I/O.
- `Engine.swift`: plans, timing, mistakes, repairs and progress. Environmental
  effects go through `EngineRuntime`; `VirtualRuntime` never posts input.
- `Telemetry.swift`: the Windows TSV schema and escaping.
- `Capture.swift`, `Analysis.swift`, `Replay.swift`, `Passage.swift`: recording,
  edit alignment, estimators, nine-seed forward forecasts and results wording.
- `MacRuntime.swift`: cancellable absolute-deadline waits, the App Nap activity,
  window identity, literal event encoding, modifier guards, Accessibility checks
  and Carbon F7 registration. `KeyboardEncoder` constructs events; only the
  explicitly supplied production sink posts them.
- `Windows.swift`, `CalibrationWindows.swift`: real AppKit windows. Tests inject
  permissions, saving and a runtime; diagnostic windows are never ordered front.
- `LiveTypingProbe.swift`: the explicit guarded desktop proof. Its default is
  refusal: arguments, isolated data, idle time, unlocked console, permission,
  owned focus and deadlines must all pass before it sends input.

Porting rules worth preserving: use UTF-16 units throughout the model, preserve
arithmetic order in estimators and replay, and do not use dictionary iteration
for numeric accumulation or random selection. The residual covariance explicitly
walks event insertion order. The analysis's missed-Shift mapping is copied
verbatim even where its punctuation order differs from the engine's mapping.

Reference readers live only in `Tests/`. The application never reads reference
outputs. Tests decode reference escapes to UTF-16 without replacing surrogate
halves. They compare engine logs and hashes exactly, all result text exactly,
and unformatted analysis doubles within relative 1e-9. Telemetry comparisons
replace only the three wall-clock columns described in `reference/README.md`.

Offscreen window rendering uses AppKit's view cache, not a desktop screenshot.
A logged-in graphical Mac session is needed for native-control rendering and the
background timing assertion. A restrictive execution sandbox may block Apple's
`iconutil` or native control rendering; run the scripts in an ordinary Terminal
session on the Mac. Do not change privacy permissions to run automated tests.

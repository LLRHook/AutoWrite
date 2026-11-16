import AppKit

func eventUnicode(_ event: CGEvent) -> [Unit] {
    var actual = 0, chars = [Unit](repeating: 0, count: 64)
    event.keyboardGetUnicodeString(maxStringLength: chars.count, actualStringLength: &actual, unicodeString: &chars)
    return Array(chars.prefix(actual))
}
final class TimedRuntime: EngineRuntime {
    let cancellation = Cancellation()
    lazy var pauses = PrecisePauses(cancellation)
    var keys = [CaptureKey](), start = 0.0
    var background = true
    let seed: Int
    init(seed: Int) { self.seed = seed; _ = pauses }
    func createRandom() -> DotRandom { DotRandom(seed) }
    func foregroundWindow() throws -> UInt64 { 0 }
    func isKeyDown(_ key: Int) throws -> Bool { false }
    func releaseModifiers() throws { pauses.reset() }
    func sendCharacter(_ value: Unit) throws {
        let now = ProcessInfo.processInfo.systemUptime
        if keys.isEmpty { start = now }
        background = background && NSWorkspace.shared.frontmostApplication?.processIdentifier != ProcessInfo.processInfo.processIdentifier
        keys.append(CaptureKey(milliseconds: (now - start) * 1000, character: value))
    }
    func sleep(_ milliseconds: Int) throws { try pauses.sleep(milliseconds) }
    var milliseconds: Double { ProcessInfo.processInfo.systemUptime * 1000 }
}
extension TestRunner {
    func system(_ fixtures: Fixtures, _ evidence: URL) {
        test("CGEvent encoding: literal Unicode, emoji pairs, Tab, Enter and Backspace; zero posts") {
            let encoder = KeyboardEncoder()
            let text = units("aBZ09!@#$%^&*()[]{}<>;:'\"/?\\|_=+`~ café — “quote” 😀\t\n")
            var unicode = [Unit](), special = [Int]()
            for c in text {
                let events = encoder.events(for: c)
                if (0xD800...0xDBFF).contains(c) { try require(events.isEmpty, "surrogate high posted alone"); continue }
                try equal(events.count, 2, "down/up pair")
                try equal(events[0].type, .keyDown, "down"); try equal(events[1].type, .keyUp, "up")
                for event in events { try equal(event.flags.rawValue, 0, "modifier flags") }
                let chars = eventUnicode(events[0])
                try equal(eventUnicode(events[1]), chars, "Unicode on keyup")
                if c == 9 || c == 10 { special.append(Int(events[0].getIntegerValueField(.keyboardEventKeycode))) }
                else { unicode += chars }
            }
            try equal(unicode, text.filter { $0 != 9 && $0 != 10 }, "literal characters")
            try equal(special, [48, 36], "Tab/Enter virtual keys")
            let backspaces = encoder.events(for: 8)
            try equal(backspaces[0].getIntegerValueField(.keyboardEventKeycode), 51, "Backspace key")
            let emoji = KeyboardEncoder(); _ = emoji.events(for: 0xD83D); _ = emoji.events(for: 0xDE00)
            try equal(emoji.events(for: 8).count, 2, "emoji Backspace")
            try require(emoji.events(for: 8).isEmpty, "second UTF16 half deletes a second character")
        }
        test("reference event transport: all 223 runs repair to the exact intended text; zero posts") {
            let rows = try fixtures.rows("engine.tsv")
            for row in rows {
                let encoder = KeyboardEncoder(), runtime = VirtualRuntime(seed: Int(row[1])!)
                var received = [Unit]()
                runtime.onSend = { value, _ in
                    for event in encoder.events(for: value) where event.type == .keyDown {
                        switch event.getIntegerValueField(.keyboardEventKeycode) {
                        case 51:
                            if let last = received.popLast(), (0xDC00...0xDFFF).contains(last), let high = received.last, (0xD800...0xDBFF).contains(high) { received.removeLast() }
                        case 36: received.append(10)
                        case 48: received.append(9)
                        default: received += eventUnicode(event)
                        }
                    }
                }
                try TypingEngine(runtime).type(unescape(row[7]), options: TypingOptions(settings: Settings(line: row[6])))
                let expected = string(unescape(row[7])).replacingOccurrences(of: "\r\n", with: "\n").replacingOccurrences(of: "\r", with: "\n")
                try equal(received, units(expected), "transport \(row[0])")
            }
            print("REFERENCE system event transport: \(rows.count) engine runs compared; posts=0")
        }
        test("held Command cannot turn q into a shortcut; modifiers rechecked per key") {
            let cancel = Cancellation(), runtime = MacRuntime(cancellation: cancel, seed: 1, sink: { _ in })
            var checks = 0, events = [CGEvent]()
            runtime.modifierDown = { checks += 1; return checks < 4 }
            runtime.windowIdentity = { 123 }; runtime.expectedWindow = 123
            runtime.sink = { event in events.append(event) }
            try runtime.sendCharacter(113)
            try require(checks >= 4, "did not wait for Command release")
            try equal(events.count, 2, "literal q event pair")
            try equal(eventUnicode(events[0]), [113], "q")
            try equal(events[0].flags.rawValue, 0, "Command leaked")
            checks = 0; try runtime.sendCharacter(97); try require(checks >= 4, "only checked modifiers at run start")
        }
        test("target window loss pauses posting; cancellation stops before next event") {
            let cancel = Cancellation(), runtime = MacRuntime(cancellation: cancel, seed: 1, sink: { _ in })
            var checks = 0, posts = 0
            runtime.expectedWindow = 100; runtime.modifierDown = { false }
            runtime.windowIdentity = { checks += 1; return checks < 3 ? 200 : 100 }
            runtime.sink = { _ in posts += 1 }
            try runtime.sendCharacter(97)
            try require(checks >= 3 && posts == 2, "focus guard did not wait")
            cancel.cancel()
            do { try runtime.sendCharacter(98); throw TestFailure(description: "posted after cancellation") } catch AutoWriteError.cancelled {}
            try equal(posts, 2, "cancelled input")
        }
        test("live proof safety gates, argument exits, fixed seed corrections and redacted differences") {
            for args in [[], ["--live-typing"], ["--live-typing", "--require-idle-seconds", "239"], ["--live-typing", "--require-idle-seconds", "x"], ["--live-typing", "--require-idle-seconds", "86401"]] { try require(LiveTypingProbe.parse(args) == nil, "bad arguments accepted") }
            try equal(LiveTypingProbe.parse(["--live-typing", "--require-idle-seconds", "240"]), 240, "valid idle threshold")
            try require(!LiveGate(idle: 239, onConsole: true, unlocked: true).eligible(required: 240), "active gate")
            try require(!LiveGate(idle: 300, onConsole: true, unlocked: false).eligible(required: 240), "locked gate")
            try require(!LiveGate(idle: 300, onConsole: false, unlocked: true).eligible(required: 240), "console gate")
            let r = VirtualRuntime(seed: 751)
            try TypingEngine(r).type(LiveTypingProbe.expected, options: TypingOptions(settings: LiveTypingProbe.settings()))
            var text = [Unit]()
            for key in r.keys { if key.character == 8 { if !text.isEmpty { text.removeLast() } } else { text.append(key.character) } }
            try equal(text, LiveTypingProbe.expected, "smoke planned final text")
            try require(r.keys.contains { $0.character == 8 }, "smoke makes no mistake")
            try equal(LiveTypingProbe.difference([97, 98], [97, 99]), "expected_length=2; received_length=2; first_difference=1", "redacted difference")
            try require(r.time < 11000, "smoke exceeds watchdog")
            let gate = LiveGate.read()
            let precheck = "LIVE PRECHECK: idle_seconds=\(fixed(gate.idle, 3)); unlocked=\(dotBool(gate.unlocked)); on_console=\(dotBool(gate.onConsole)); permission=\(AccessibilityPermission().state())"
            print(precheck); try precheck.write(to: evidence.appendingPathComponent("live-precheck.txt"), atomically: true, encoding: .utf8)
        }
        test("background real pauses: median error <2 ms, no key >20 ms late; no posts") {
            var settings = Settings()
            for spec in Settings.specs where !spec.isBool { settings[spec.name] = 0 }
            settings["WordsPerMinute"] = 180; settings["EnableTypos"] = 0; settings["KeepTargetWpm"] = 1
            let text = units("thequickbrownfoxjumpsoverthelazydogthequickbrownfox"), virtual = VirtualRuntime(seed: 17)
            try TypingEngine(virtual).type(text, options: TypingOptions(settings: settings))
            let real = TimedRuntime(seed: 17), activity = TimingActivity(); defer { activity.end() }
            try TypingEngine(real).type(text, options: TypingOptions(settings: settings))
            try equal(real.keys.count, virtual.keys.count, "timing key count")
            let errors = zip(real.keys, virtual.keys).map { $0.milliseconds - $1.milliseconds }
            let median = CalibrationAnalysis.median(errors.map(abs)), worst = errors.max() ?? 0, worstAbs = errors.map(abs).max() ?? 0
            let report = "TIMING: keys=\(real.keys.count); background=\(dotBool(real.background)); median_absolute_error_ms=\(fixed(median, 3)); worst_late_ms=\(fixed(worst, 3)); worst_absolute_error_ms=\(fixed(worstAbs, 3)); duration_ms=\(virtual.time); posts=0"
            print(report); try report.write(to: evidence.appendingPathComponent("timing.txt"), atomically: true, encoding: .utf8)
            try require(real.background, "timing process was foreground")
            try require(median < 2 && worst <= 20, "real pauses miss timing limits")
        }
    }
}

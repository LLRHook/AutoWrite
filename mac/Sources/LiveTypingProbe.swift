import AppKit
import ApplicationServices

struct LiveGate {
    let idle: Double, onConsole: Bool, unlocked: Bool
    static func read() -> LiveGate {
        let session = CGSessionCopyCurrentDictionary() as? [String: Any]
        let onConsole = session?[kCGSessionOnConsoleKey as String] as? Bool == true && session?[kCGSessionLoginDoneKey as String] as? Bool == true
        return LiveGate(idle: CGEventSource.secondsSinceLastEventType(.combinedSessionState, eventType: CGEventType(rawValue: UInt32.max)!), onConsole: onConsole, unlocked: session != nil && session?["CGSSessionScreenIsLocked"] as? Bool != true)
    }
    func eligible(required: Int) -> Bool { onConsole && unlocked && idle.isFinite && idle >= Double(required) }
}
enum LiveTypingProbe {
    static let expected = units("AutoWrite: aB [12] (3.5%)?\nOK")
    static func settings() -> Settings {
        var s = Settings()
        for name in ["JitterPercent", "BurstPercent", "KeyPairRhythmPercent", "WordPauseMilliseconds", "SentencePauseMilliseconds", "ParagraphPauseMilliseconds", "ThinkingPauseChancePercent", "CorrectionDetectChars", "UppercaseDelayPenaltyPercent", "NumberDelayPenaltyPercent", "PunctuationDelayPenaltyPercent", "ShiftSymbolDelayPenaltyPercent"] { s[name] = 0 }
        s["WordsPerMinute"] = 180; s["EnableTypos"] = 1; s["TypoChancePercent"] = 10
        s["CorrectionNoticeMilliseconds"] = 50; s["CorrectionPauseMilliseconds"] = 20; s["CorrectionResumeMilliseconds"] = 20
        return s
    }
    static func difference(_ expected: [Unit], _ received: [Unit]) -> String {
        var i = 0
        while i < min(expected.count, received.count) && expected[i] == received[i] { i += 1 }
        let difference = i == expected.count && i == received.count ? -1 : i
        return "expected_length=\(expected.count); received_length=\(received.count); first_difference=\(difference)"
    }
    static func parse(_ arguments: [String]) -> Int? {
        guard arguments.count == 3, arguments[0] == "--live-typing", arguments[1] == "--require-idle-seconds", let n = parseInt(arguments[2]), n >= 240 && n <= 86400 else { return nil }
        return n
    }
    static func run(arguments: [String]) -> Int32 {
        guard let required = parse(arguments) else { print("exit=4; usage: --live-typing --require-idle-seconds N (240..86400)"); return 4 }
        guard let override = ProcessInfo.processInfo.environment["AUTOWRITE_DATA_DIR"], override.hasPrefix("/"), let directory = try? dataDirectory() else { print("exit=4; an isolated absolute AUTOWRITE_DATA_DIR is required"); return 4 }
        func outcome(_ text: String, _ code: Int32) -> Int32 {
            try? FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
            try? text.write(to: directory.appendingPathComponent("live-probe.txt"), atomically: true, encoding: .utf8)
            print(text); return code
        }
        let gate = LiveGate.read()
        let idle = "idle_seconds=\(fixed(gate.idle, 3)); required_idle_seconds=\(required)"
        guard gate.eligible(required: required) else { return outcome("exit=3; skipped: user active or screen locked; \(idle); no window or input created", 3) }
        guard AccessibilityPermission().state() == .allowed else { return outcome("exit=2; Accessibility permission missing or restart required; no window or input created", 2) }
        // Only this explicit mode creates/activates a proof window. It never logs
        // received content; the engine's telemetry is disabled as well.
        let app = NSApplication.shared; app.setActivationPolicy(.accessory)
        let previous = NSWorkspace.shared.frontmostApplication
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 540, height: 220), styleMask: [.titled], backing: .buffered, defer: false)
        window.title = "AutoWrite owned typing smoke check"; window.isReleasedWhenClosed = false
        let input = LiteralTextView(); fill(textScroll(input), in: window.contentView!)
        window.center()
        // Check idle and lock state again immediately before the focus change.
        guard LiveGate.read().eligible(required: required) else { window.close(); return outcome("exit=3; skipped: user active or screen locked; no input sent", 3) }
        let start = ProcessInfo.processInfo.systemUptime
        window.makeKeyAndOrderFront(nil); app.activate(ignoringOtherApps: true); window.makeFirstResponder(input)
        let cancellation = Cancellation()
        var started = false, done = false, failed = false, backspaces = 0, hotkeyStarted = false
        let lock = NSLock()
        let ownPID = ProcessInfo.processInfo.processIdentifier
        func ownsFocus() -> Bool { NSWorkspace.shared.frontmostApplication?.processIdentifier == ownPID && window.isKeyWindow && window.firstResponder === input }
        let ownedIdentity = UInt64(UInt32(window.windowNumber)) << 32 | UInt64(UInt32(bitPattern: ownPID))
        func startTyping() {
            guard !started, ownsFocus(), ProcessInfo.processInfo.systemUptime - start <= 0.5 else { return }; started = true
            DispatchQueue.global(qos: .userInteractive).async {
                let activity = TimingActivity(); defer { activity.end() }
                let runtime = MacRuntime(cancellation: cancellation, seed: 751)
                runtime.expectedWindow = ownedIdentity
                runtime.guardBeforePost = {
                    if ProcessInfo.processInfo.systemUptime - start >= 11 || foregroundWindowIdentity() != ownedIdentity { throw AutoWriteError.cancelled }
                }
                let original = runtime.sink
                runtime.sink = { event in
                    if event.type == .keyDown && event.getIntegerValueField(.keyboardEventKeycode) == 51 { lock.lock(); backspaces += 1; lock.unlock() }
                    original(event)
                }
                do { try TypingEngine(runtime).type(expected, options: TypingOptions(settings: settings())) }
                catch { lock.lock(); failed = true; lock.unlock() }
                lock.lock(); done = true; lock.unlock()
            }
        }
        let hotkey = GlobalHotkey { if !started { hotkeyStarted = true; startTyping() } }
        let armed = hotkey.register()
        var sentF7 = false
        while ProcessInfo.processInfo.systemUptime - start < 12 {
            while let event = app.nextEvent(matching: .any, until: Date(timeIntervalSinceNow: 0.005), inMode: .default, dequeue: true) { app.sendEvent(event); if ProcessInfo.processInfo.systemUptime - start >= 12 { break } }
            let elapsed = ProcessInfo.processInfo.systemUptime - start
            lock.lock(); let finished = done; lock.unlock()
            if finished { break }
            if !started && elapsed >= 0.5 { cancellation.cancel(); failed = true; break }
            if !ownsFocus() {
                if started || elapsed >= 0.5 { cancellation.cancel(); failed = true; break }
                continue
            }
            if !sentF7 && armed && !MacRuntime.modifierKeys.contains(where: { CGEventSource.keyState(.hidSystemState, key: CGKeyCode($0)) }) {
                sentF7 = true
                for down in [true, false] {
                    guard ownsFocus(), let event = CGEvent(keyboardEventSource: CGEventSource(stateID: .privateState), virtualKey: 98, keyDown: down) else { failed = true; break }
                    event.flags = []; event.post(tap: .cghidEventTap)
                }
            }
            if !started && elapsed > 0.45 { startTyping() }
        }
        cancellation.cancel(); hotkey.unregister()
        let settle = ProcessInfo.processInfo.systemUptime + 0.5
        while started && ProcessInfo.processInfo.systemUptime < settle { lock.lock(); let finished = done; lock.unlock(); if finished { break }; RunLoop.current.run(until: Date(timeIntervalSinceNow: 0.005)) }
        // Drain keys already posted to our own queue before comparing.
        while let event = app.nextEvent(matching: .any, until: Date(), inMode: .default, dequeue: true) { app.sendEvent(event) }
        lock.lock(); let complete = done, errors = failed, repairs = backspaces; lock.unlock()
        let received = units(input.string.replacingOccurrences(of: "\r\n", with: "\n")), hadFocus = ownsFocus()
        window.close()
        if hadFocus { previous?.activate(options: []) }
        let passed = started && complete && !errors && hadFocus && repairs > 0 && received == expected
        let code: Int32 = passed ? 0 : 2
        return outcome("exit=\(code); elapsed_ms=\(Int((ProcessInfo.processInfo.systemUptime - start) * 1000)); \(idle); hotkey=\(hotkeyStarted ? "F7" : "direct-fallback"); backspaces_sent=\(repairs); \(difference(expected, received)); \(passed ? "PASS exact text through real CGEvent" : "failed, focus lost, or timeout")", code)
    }
}

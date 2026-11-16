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
    enum Invocation: Equatable {
        case automation(Int), owner
        var idleSeconds: Int? { if case .automation(let seconds) = self { return seconds }; return nil }
    }
    static func parse(_ arguments: [String]) -> Invocation? {
        if arguments == ["--live-typing"] { return .owner }
        if arguments.count == 3 && arguments[0] == "--live-typing" && arguments[1] == "--owner-proof-directory" && arguments[2].hasPrefix("/") { return .owner }
        guard arguments.count == 3, arguments[0] == "--live-typing", arguments[1] == "--require-idle-seconds", let n = Int(arguments[2]), n >= 240 && n <= 86400 else { return nil }
        return .automation(n)
    }
    static func run(arguments: [String], permission: AccessibilityPermission = AccessibilityPermission(), gateRead: () -> LiveGate = LiveGate.read,
                    ownerExplanation: (AccessibilityPermission, PermissionState) -> Void = explainOwnerPermission,
                    proof: ((URL, Int?) -> Int32)? = nil,
                    runningCopies: () -> [RunningCopy] = AutoWriteInstances.running,
                    now: () -> TimeInterval = { Date().timeIntervalSince1970 }) -> Int32 {
        let hardDeadline = ProcessInfo.processInfo.systemUptime + 15
        guard let invocation = parse(arguments) else { print("exit=4; usage: --live-typing [--require-idle-seconds N (240..86400)]"); return 4 }
        let directory: URL
        if invocation == .owner {
            guard arguments.count == 3 else { print("exit=4; owner proof must be started through mac/smoke.sh"); return 4 }
            let work = URL(fileURLWithPath: arguments[2])
            guard let text = try? String(contentsOf: work.appendingPathComponent("deadline"), encoding: .utf8), let deadline = Double(trim(text)), now() < deadline else {
                print("exit=2; owner proof launch expired or was not prepared; no window or input created"); return 2
            }
            directory = work.appendingPathComponent("data")
            try? String(ProcessInfo.processInfo.processIdentifier).write(to: work.appendingPathComponent("proof.pid"), atomically: true, encoding: .utf8)
        } else {
            guard let override = ProcessInfo.processInfo.environment["AUTOWRITE_DATA_DIR"], override.hasPrefix("/"), let value = try? dataDirectory() else { print("exit=4; an isolated absolute AUTOWRITE_DATA_DIR is required"); return 4 }
            directory = value
        }
        func refuse(_ code: Int32, _ text: String) -> Int32 { outcome(directory, text, code) }
        if AutoWriteInstances.other(runningCopies()) != nil {
            return refuse(2, "exit=2; quit AutoWrite first, then run the live proof; no window or input created")
        }
        let gate = gateRead(), required = invocation.idleSeconds
        guard gate.eligible(required: required ?? 0) else { return refuse(3, "exit=3; skipped: user active or screen locked; idle_seconds=\(fixed(gate.idle, 3)); no window or input created") }
        let state = permission.state()
        guard state == .allowed else {
            if invocation == .owner { permission.requestIfMissing(); ownerExplanation(permission, state) }
            return refuse(2, "exit=2; Accessibility permission missing or restart required; no typing window or input created")
        }
        return proof?(directory, required) ?? perform(directory: directory, required: required, deadline: hardDeadline)
    }
    private static func outcome(_ directory: URL, _ text: String, _ code: Int32) -> Int32 {
        try? FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        try? text.write(to: directory.appendingPathComponent("live-probe.txt"), atomically: true, encoding: .utf8)
        print(text); return code
    }
    static func explainOwnerPermission(_ permission: AccessibilityPermission, _ state: PermissionState) {
        let app = NSApplication.shared; app.setActivationPolicy(.accessory)
        let dialog = PermissionWindow(permission: permission, state: state)
        dialog.window?.center(); dialog.showWindow(nil); app.activate(ignoringOtherApps: true)
        let deadline = Date(timeIntervalSinceNow: 10)
        while Date() < deadline && dialog.window?.isVisible == true {
            if let event = app.nextEvent(matching: .any, until: Date(timeIntervalSinceNow: 0.02), inMode: .default, dequeue: true) { app.sendEvent(event) }
        }
        dialog.cancel()
    }
    private static func perform(directory: URL, required: Int?, deadline: Double) -> Int32 {
        func outcome(_ text: String, _ code: Int32) -> Int32 { self.outcome(directory, text, code) }
        let gate = LiveGate.read()
        let idle = required.map { "idle_seconds=\(fixed(gate.idle, 3)); required_idle_seconds=\($0)" } ?? "owner_launch=True"
        // Only this explicit mode creates/activates a proof window. It never logs
        // received content; the engine's telemetry is disabled as well.
        let app = NSApplication.shared; app.setActivationPolicy(.accessory)
        let previous = NSWorkspace.shared.frontmostApplication
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 540, height: 220), styleMask: [.titled], backing: .buffered, defer: false)
        window.title = "AutoWrite owned typing smoke check"; window.isReleasedWhenClosed = false
        let input = LiteralTextView(); fill(textScroll(input), in: window.contentView!)
        window.center()
        // Check idle and lock state again immediately before the focus change.
        guard ProcessInfo.processInfo.systemUptime < deadline - 1, LiveGate.read().eligible(required: required ?? 0) else { window.close(); return outcome("exit=3; skipped: user active or screen locked; no input sent", 3) }
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
                    if ProcessInfo.processInfo.systemUptime >= deadline - 1 || ProcessInfo.processInfo.systemUptime - start >= 11 || foregroundWindowIdentity() != ownedIdentity { throw AutoWriteError.cancelled }
                }
                let original = runtime.sink
                runtime.sink = { event in
                    if event.type == .keyDown && event.getIntegerValueField(.keyboardEventKeycode) == 51 { lock.lock(); backspaces += 1; lock.unlock() }
                    original(event)
                }
                do {
                    var options = TypingOptions(settings: settings()); options.triggerKey = 98
                    try TypingEngine(runtime).type(expected, options: options)
                }
                catch { lock.lock(); failed = true; lock.unlock() }
                lock.lock(); done = true; lock.unlock()
            }
        }
        let hotkey = GlobalHotkey { if !started { hotkeyStarted = true; startTyping() } }
        let armed = hotkey.register()
        var sentF7 = false
        while ProcessInfo.processInfo.systemUptime < deadline - 0.5 && ProcessInfo.processInfo.systemUptime - start < 12 {
            while let event = app.nextEvent(matching: .any, until: Date(timeIntervalSinceNow: 0.005), inMode: .default, dequeue: true) { app.sendEvent(event); if ProcessInfo.processInfo.systemUptime >= deadline - 0.5 || ProcessInfo.processInfo.systemUptime - start >= 12 { break } }
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
                let events = GlobalHotkey.triggerEvents()
                guard ownsFocus(), events.count == 2 else { failed = true; break }
                for event in events { event.post(tap: .cghidEventTap) }
            }
            if !started && elapsed > 0.45 { startTyping() }
        }
        cancellation.cancel(); hotkey.unregister()
        let settle = min(deadline, ProcessInfo.processInfo.systemUptime + 0.5)
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

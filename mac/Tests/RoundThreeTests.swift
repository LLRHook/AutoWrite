import AppKit

private func pump(until condition: () -> Bool, seconds: Double = 3) -> Bool {
    let deadline = Date(timeIntervalSinceNow: seconds)
    while !condition() && Date() < deadline { RunLoop.current.run(until: Date(timeIntervalSinceNow: 0.005)) }
    return condition()
}
private final class ClickProbe: NSView {
    var clicks = 0
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }
    override func mouseDown(with event: NSEvent) { clicks += 1 }
}
private func click(_ window: NSWindow, _ view: NSView) {
    let point = view.convert(NSPoint(x: view.bounds.midX, y: view.bounds.midY), to: nil)
    let event = NSEvent.mouseEvent(with: .leftMouseDown, location: point, modifierFlags: [], timestamp: ProcessInfo.processInfo.systemUptime, windowNumber: window.windowNumber, context: nil, eventNumber: 1, clickCount: 1, pressure: 0)!
    // Directly deliver to this test's window. Nothing enters the system queue.
    NSApplication.shared.sendEvent(event)
}

extension TestRunner {
    func roundThree(_ directory: URL, _ evidence: URL) throws {
        test("invalid number edits preserve every field and persisted value; fractional multipliers remain valid") {
            let data = directory.appendingPathComponent("invalid-numbers")
            let main = try MainWindow(directory: data, diagnostic: true)
            defer { main.shutdown(); main.window?.close() }
            let original = main.currentSettings()
            for control in main.orderedControls where !control.spec.isBool {
                let field = control.field!, before = original[control.spec.name]
                for text in ["abc", "", "1,000"] + (control.spec.isMultiplier ? [] : ["12.5"]) {
                    field.stringValue = text
                    control.controlTextDidChange(Notification(name: NSControl.textDidChangeNotification, object: field))
                    try equal(control.value, before, "invalid draft changed \(control.spec.name)")
                    control.controlTextDidEndEditing(Notification(name: NSControl.textDidEndEditingNotification, object: field))
                    try equal(control.value, before, "invalid edit changed \(control.spec.name)")
                    try equal(field.doubleValue, Double(before) / (control.spec.isMultiplier ? 100 : 1), "previous display not restored")
                }
                if control.spec.isMultiplier {
                    field.stringValue = "1.5"
                    control.controlTextDidEndEditing(Notification(name: NSControl.textDidEndEditingNotification, object: field))
                    try equal(control.value, 150, "valid multiplier rejected")
                    try equal(field.stringValue, "1.50", "multiplier precision")
                    control.value = before
                }
            }
            main.shutdown()
            try equal(try Settings.load(directory: data), original, "invalid edits saved on quit")
            // Exercise AppKit's real formatter/field-editor commit too, rather
            // than only delivering a delegate notification.
            let control = main.controls["WordsPerMinute"]!, field = control.field!
            for text in ["abc", "", "12.5", "1,000", "999"] {
                try require(main.window!.makeFirstResponder(field), "cannot begin numeric editing")
                let editor = main.window!.fieldEditor(false, for: field) as! NSTextView
                editor.string = text
                try require(main.window!.makeFirstResponder(nil), "formatter trapped invalid edit")
                try equal(control.value, text == "999" ? control.spec.maximum : original["WordsPerMinute"], "AppKit field editor commit")
            }
        }
        test("second normal launch activates the first copy and exits; owner proof refuses running copies") {
            let own = ProcessInfo.processInfo.processIdentifier
            let copies = [RunningCopy(pid: own, bundleIdentifier: AutoWriteInstances.bundleIdentifier), RunningCopy(pid: 99991, bundleIdentifier: "other.app"), RunningCopy(pid: 99992, bundleIdentifier: AutoWriteInstances.bundleIdentifier)]
            var activated = [pid_t](), exits = 0
            let delegate = ApplicationDelegate()
            delegate.runningCopies = { copies }; delegate.activateCopy = { activated.append($0) }; delegate.terminate = { exits += 1 }
            delegate.applicationDidFinishLaunching(Notification(name: NSApplication.didFinishLaunchingNotification))
            try equal(activated, [99992], "did not activate first copy")
            try equal(exits, 1, "second copy did not exit")
            try require(delegate.main == nil, "second copy opened a window or armed F7")
            try require(AutoWriteInstances.other(Array(copies.prefix(2))) == nil, "own process or another product blocks launch")
            let work = directory.appendingPathComponent("owner-instance"); try FileManager.default.createDirectory(at: work, withIntermediateDirectories: true)
            try String(Date().timeIntervalSince1970 + 30).write(to: work.appendingPathComponent("deadline"), atomically: true, encoding: .utf8)
            var requests = 0, proofs = 0
            let permission = AccessibilityPermission(trusted: { false }, canPost: { false }, requestListing: { requests += 1 })
            let code = LiveTypingProbe.run(arguments: ["--live-typing","--owner-proof-directory",work.path], permission: permission, gateRead: { LiveGate(idle: 0, onConsole: true, unlocked: true) }, ownerExplanation: { _, _ in throwawayFailure() }, proof: { _, _ in proofs += 1; return 0 }, runningCopies: { copies })
            try equal(code, 2, "owner allowed alongside normal app")
            try require(requests == 0 && proofs == 0, "instance refusal requested permission or started proof")
            let message = try String(contentsOf: work.appendingPathComponent("data/live-probe.txt"), encoding: .utf8)
            try require(message.contains("quit AutoWrite first"), "missing owner instruction")
        }
        test("proof F7 event pair carries the function flag and matching key code; zero posts") {
            let events = GlobalHotkey.triggerEvents()
            try equal(events.map { $0.type }, [.keyDown,.keyUp], "F7 pair")
            for event in events {
                try equal(event.getIntegerValueField(.keyboardEventKeycode), 98, "F7 key code")
                try require(event.flags.contains(.maskSecondaryFn), "F7 function flag absent")
                try require(event.flags.intersection([.maskCommand,.maskControl,.maskAlternate,.maskShift]).isEmpty, "shortcut modifier in proof")
                try equal(event.getIntegerValueField(.keyboardEventAutorepeat), 0, "proof is repeat")
            }
        }
        test("app hotkey path waits for F7 release before any typing; zero posts") {
            let main = try MainWindow(directory: directory.appendingPathComponent("trigger-release"), permission: AccessibilityPermission(trusted: { true }, canPost: { true }, requestListing: {}), diagnostic: true)
            defer { main.shutdown(); main.window?.close() }
            main.text.string = "q"
            var checks = 0, held = true, unsafe = false, posts = 0
            let real = MacRuntime(cancellation: Cancellation(), seed: 17, sink: { _ in if held { unsafe = true }; posts += 1 })
            real.windowIdentity = { 123 }
            real.physicalKeyState = { key in
                guard key == 98 else { return false }
                checks += 1; held = checks < 4; return held
            }
            main.runtimeFactory = { _ in real }
            main.requestTyping(buttonDelay: false)
            try require(pump(until: { !main.running }), "hotkey request did not finish")
            try require(checks >= 4 && !unsafe && posts > 0, "typing began before F7 released")
        }
        test("real offscreen sheets: timer expiry then results Apply, Cancel, and first main click") {
            let original = NSWorkspace.shared.frontmostApplication?.processIdentifier
            let main = try MainWindow(directory: directory.appendingPathComponent("real-sheets"), diagnostic: true, offscreenPresentation: true)
            let owner = main.window!
            defer { main.shutdown(); owner.close() }
            owner.orderBack(nil)
            let probe = ClickProbe(frame: NSRect(x: 100, y: 100, width: 20, height: 20)); owner.contentView!.addSubview(probe)
            func checkOffscreen() throws {
                try require(NSApplication.shared.modalWindow == nil, "nested application modal loop")
                for window in [owner] + owner.sheets {
                    try require(window.alphaValue == 0 && window.ignoresMouseEvents, "test window is visible or can intercept desktop input")
                }
                try equal(NSWorkspace.shared.frontmostApplication?.processIdentifier, original, "sheet activated test process")
            }
            main.openCalibration()
            let capture = main.calibration!
            try require(pump(until: { owner.attachedSheet === capture.window }), "real capture sheet did not attach")
            try checkOffscreen()
            try require(!main.windowShouldClose(owner) && !main.startButton.isEnabled, "owner usable during capture")
            capture.length.selectItem(at: 0)
            capture.input.keyDown(with: key(83, timestamp: ProcessInfo.processInfo.systemUptime - 29.9))
            // Do not call tick/finish: the production timer must end the test.
            try require(pump(until: { main.results != nil && owner.attachedSheet === main.results?.window }), "timer did not transition between actual sheets")
            try require(capture.window!.sheetParent == nil, "capture sheet left attached")
            try require(!main.windowShouldClose(owner) && !main.startButton.isEnabled, "owner usable during results")
            try checkOffscreen()
            let proposed = main.results!.result.proposed
            main.results!.applyButton.performClick(nil)
            try require(pump(until: { !main.calibrationOpen && owner.attachedSheet == nil }), "Apply left a sheet attached")
            try equal(main.currentSettings(), proposed, "real sheet Apply")
            click(owner, probe); try equal(probe.clicks, 1, "first main click swallowed after Apply")
            main.openCalibration()
            try require(pump(until: { owner.attachedSheet === main.calibration?.window }), "second capture did not attach")
            main.calibration!.cancelButton.performClick(nil)
            try require(pump(until: { !main.calibrationOpen && owner.attachedSheet == nil }), "Cancel left a sheet attached")
            click(owner, probe); try equal(probe.clicks, 2, "first main click swallowed after Cancel")
            try require(main.windowShouldClose(owner) && main.startButton.isEnabled, "owner remains locked")
            try checkOffscreen()
        }
        test("denied typing presents the actual permission sheet offscreen; restart has working Quit") {
            let original = NSWorkspace.shared.frontmostApplication?.processIdentifier
            var requests = 0
            let permission = AccessibilityPermission(trusted: { false }, canPost: { false }, requestListing: { requests += 1 })
            let main = try MainWindow(directory: directory.appendingPathComponent("real-permission-sheet"), permission: permission, diagnostic: true, offscreenPresentation: true)
            let owner = main.window!; owner.orderBack(nil)
            defer { main.permissionDialog?.cancel(); main.shutdown(); owner.close() }
            main.requestTyping(buttonDelay: false)
            guard let dialog = main.permissionDialog else { throw TestFailure(description: "no real permission controller") }
            try require(pump(until: { owner.attachedSheet === dialog.window }), "permission sheet not presented")
            try require(dialog.window?.sheetParent === owner && requests == 1 && !main.running, "permission presentation bypassed")
            try require(dialog.window!.alphaValue == 0 && dialog.window!.ignoresMouseEvents, "permission sheet is visible or intercepts input")
            try equal(NSWorkspace.shared.frontmostApplication?.processIdentifier, original, "denied diagnostic activated app")
            dialog.cancel()
            try require(pump(until: { main.permissionDialog == nil && owner.attachedSheet == nil }), "permission sheet not dismissed")
            let restart = PermissionWindow(permission: permission, state: .restartNeeded)
            var quits = 0; restart.quit = { quits += 1 }
            try require(!restart.quitButton.isHidden && restart.quitButton.title == "Quit AutoWrite", "restart message has no Quit")
            restart.quitButton.performClick(nil); try equal(quits, 1, "Quit does not act")
            try renderWindow(restart.window!, evidence.appendingPathComponent("restart-permission.png"), appearance: .aqua)
            restart.cancel()
        }
        test("scroll hints follow hidden rows, disappear at the bottom and when everything fits") {
            let main = try MainWindow(directory: directory.appendingPathComponent("scroll-dynamic"), diagnostic: true)
            defer { main.shutdown(); main.window?.close() }
            func check(_ scroll: NSScrollView, _ hint: NSTextField, _ window: NSWindow, _ last: NSView) throws {
                window.contentView?.layoutSubtreeIfNeeded()
                try require(hint.alphaValue == 1 && !hint.stringValue.isEmpty, "missing cue above hidden rows")
                try require(!hint.stringValue.contains("Difficult keys"), "hint claims visible section is hidden")
                _ = scroll.documentView!.scrollToVisible(last.frame)
                try require(hint.alphaValue == 0 && hint.stringValue.isEmpty, "hint remains after final row")
                window.setContentSize(NSSize(width: window.contentView!.bounds.width, height: 2400)); window.contentView?.layoutSubtreeIfNeeded()
                try require(hint.alphaValue == 0 && hint.stringValue.isEmpty, "hint remains when all rows fit")
            }
            for appearance in [NSAppearance.Name.aqua, .darkAqua] {
                main.window!.appearance = NSAppearance(named: appearance)
                main.window!.setContentSize(NSSize(width: 1060, height: 740)); main.window!.contentView?.layoutSubtreeIfNeeded()
                let document = main.settingsScroll.documentView!
                document.scroll(NSPoint(x: 0, y: document.isFlipped ? document.bounds.minY : document.bounds.maxY))
                try check(main.settingsScroll, main.scrollHint, main.window!, main.controls["CorrectionResumeMilliseconds"]!)
                let result = CalibrationAnalysis.analyze([], seconds: 60, current: Settings())
                let results = ResultsWindow(result: result, apply: { _ in true }); defer { results.discard() }
                results.window!.appearance = NSAppearance(named: appearance)
                let last = results.scroll.documentView!.subviews.last!
                try check(results.scroll, results.scrollHint, results.window!, last)
            }
        }
    }
}
private func throwawayFailure() { fatalError("unexpected owner explanation in instance refusal test") }

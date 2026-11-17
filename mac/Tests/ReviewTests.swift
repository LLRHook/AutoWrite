import AppKit

extension TestRunner {
    func review(_ directory: URL, _ evidence: URL) throws {
        test("permission system answers map to missing, restart needed and allowed") {
            for (trusted, canPost, expected) in [(false,false,PermissionState.missing),(false,true,.missing),(true,false,.restartNeeded),(true,true,.allowed)] {
                let permission = AccessibilityPermission(trusted: { trusted }, canPost: { canPost }, requestListing: { fatalError("real permission request in test") })
                try equal(permission.state(), expected, "permission mapping")
            }
        }
        test("permission denied app path requests listing and foreground explanation; launch banner") {
            var requests = 0, presentations = [PermissionState]()
            let permission = AccessibilityPermission(trusted: { false }, canPost: { false }, requestListing: { requests += 1 })
            let owner = try MainWindow(directory: directory.appendingPathComponent("permission"), permission: permission, diagnostic: true)
            defer { owner.shutdown(); owner.window?.close() }
            owner.permissionPresentation = { presentations.append($0) }
            try require(!owner.permissionButton.isHidden && owner.permissionNotice.stringValue.contains("Accessibility") && owner.status.stringValue == "Accessibility permission required", "launch permission state missing")
            try equal(requests, 0, "launch raised permission prompt")
            try renderWindow(owner.window!, evidence.appendingPathComponent("launch-permission-light.png"), appearance: .aqua)
            try renderWindow(owner.window!, evidence.appendingPathComponent("launch-permission-dark.png"), appearance: .darkAqua)
            owner.requestTyping(buttonDelay: false)
            try equal(requests, 1, "empty-text hotkey denial did not request Accessibility listing")
            try equal(presentations, [.missing], "empty-text background denial not presented")
            owner.text.string = "q"; owner.requestTyping(buttonDelay: false)
            try equal(requests, 2, "denied app path did not request Accessibility listing")
            try equal(presentations, [.missing,.missing], "background denial not presented")
            try require(!owner.running, "denied request started typing")
            permission.trusted = { true }; permission.canPost = { false }; owner.requestTyping(buttonDelay: true)
            try equal(presentations.last, .restartNeeded, "restart-needed explanation unreachable")
            try equal(requests, 2, "restart case requested redundant prompt")
            permission.canPost = { true }; owner.refreshPermissionStatus()
            try require(owner.permissionButton.isHidden, "allowed launch banner still visible")
        }
        test("key pairs complete even when cancellation or focus guard changes after down") {
            let cancellation = Cancellation()
            var posted = [CGEventType](), guards = 0
            let runtime = MacRuntime(cancellation: cancellation, seed: 1, sink: { event in
                posted.append(event.type)
                if event.type == .keyDown { cancellation.cancel() }
            })
            runtime.expectedWindow = 1; runtime.windowIdentity = { 1 }; runtime.modifierDown = { false }
            runtime.guardBeforePost = { guards += 1; if guards > 1 { throw AutoWriteError.cancelled } }
            try runtime.sendCharacter(113)
            try equal(posted, [.keyDown,.keyUp], "key left held")
            do { try runtime.sendCharacter(97); throw TestFailure(description: "cancelled pair posted") } catch AutoWriteError.cancelled {}
        }
        test("Quit cancellation waits for the matching key-up before it can return") {
            let token = Cancellation(), down = DispatchSemaphore(value: 0), allowUp = DispatchSemaphore(value: 0), pairDone = DispatchSemaphore(value: 0), quitReturned = DispatchSemaphore(value: 0)
            var events = [CGEventType]()
            let runtime = MacRuntime(cancellation: token, seed: 1, sink: { event in
                events.append(event.type)
                if event.type == .keyDown { down.signal(); _ = allowUp.wait(timeout: .now() + 1) }
            })
            runtime.expectedWindow = 1; runtime.windowIdentity = { 1 }; runtime.modifierDown = { false }
            DispatchQueue.global().async { try? runtime.sendCharacter(97); pairDone.signal() }
            defer { allowUp.signal() }
            try require(down.wait(timeout: .now() + 1) == .success, "no down event")
            DispatchQueue.global().async { token.cancel(); quitReturned.signal() }
            try require(quitReturned.wait(timeout: .now() + 0.02) == .timedOut, "Quit returned while key was still held")
            allowUp.signal()
            try require(pairDone.wait(timeout: .now() + 1) == .success && quitReturned.wait(timeout: .now() + 1) == .success, "pair/quit failed to finish")
            try equal(events, [.keyDown,.keyUp], "Quit split pair")
        }
        test("modifier pressed during window wait is checked immediately before posting") {
            var windows = 0, modifierChecks = 0, held = false, unsafePost = false
            let runtime = MacRuntime(cancellation: Cancellation(), seed: 1, sink: { _ in if held { unsafePost = true } })
            runtime.expectedWindow = 1
            runtime.windowIdentity = { windows += 1; if windows == 1 { return 2 }; if windows == 2 { held = true }; return 1 }
            runtime.modifierDown = {
                if held { modifierChecks += 1; if modifierChecks >= 4 { held = false } }
                return held
            }
            try runtime.sendCharacter(113)
            try require(windows >= 2 && modifierChecks >= 4 && !unsafePost, "modifier was not rechecked after target wait")
        }
        test("Fn physical state participates in the production held-modifier predicate") {
            var checks = 0, posted = 0
            let runtime = MacRuntime(cancellation: Cancellation(), seed: 1, sink: { _ in posted += 1 })
            runtime.expectedWindow = 1; runtime.windowIdentity = { 1 }
            runtime.physicalKeyState = { code in if code != 63 { return false }; checks += 1; return checks < 4 }
            try runtime.sendCharacter(97)
            try require(checks >= 4 && posted == 2, "Fn was omitted from modifier checks")
        }
        test("real pause class resets after a wait instead of bursting to catch up") {
            let pause = PrecisePauses(Cancellation())
            try pause.sleep(10)
            Thread.sleep(forTimeInterval: 0.05)
            try pause.suspend(5)
            let start = ProcessInfo.processInfo.systemUptime
            try pause.sleep(30)
            let elapsed = (ProcessInfo.processInfo.systemUptime - start) * 1000
            try require(elapsed >= 25, "pause after a human wait caught up in a burst: \(elapsed) ms")
        }
        test("hotkey callback fires once per physical press until release") {
            var calls = 0
            let hotkey = GlobalHotkey { calls += 1 }
            hotkey.handle(pressed: true); hotkey.handle(pressed: true); hotkey.handle(pressed: true)
            try equal(calls, 1, "hotkey autorepeat retriggered")
            hotkey.handle(pressed: false); hotkey.handle(pressed: true); hotkey.handle(pressed: true)
            try equal(calls, 2, "next hotkey press did not fire once")
        }
        test("Stop cancels a 200000-character preparation within 100 ms") {
            let cancellation = Cancellation(), entered = DispatchSemaphore(value: 0), done = DispatchSemaphore(value: 0), lock = NSLock()
            var first = true, sent = 0, cancelled = false
            let runtime = MacRuntime(cancellation: cancellation, seed: 17, sink: { _ in sent += 1 })
            runtime.windowIdentity = { 0 }; runtime.modifierDown = { false }
            runtime.preparationCheckpoint = { lock.lock(); if first { first = false; entered.signal() }; lock.unlock() }
            var settings = Settings(); settings["TypoChancePercent"] = 10
            let text = units(String(repeating: "a", count: 200000))
            DispatchQueue.global(qos: .userInitiated).async {
                do { try TypingEngine(runtime).type(text, options: TypingOptions(settings: settings)) }
                catch AutoWriteError.cancelled { cancelled = true } catch {}
                done.signal()
            }
            try require(entered.wait(timeout: .now() + 2) == .success, "preparation never started")
            Thread.sleep(forTimeInterval: 0.025)
            let start = ProcessInfo.processInfo.systemUptime; cancellation.cancel()
            try require(done.wait(timeout: .now() + 0.1) == .success, "preparation ignored Stop for more than 100 ms")
            try require(cancelled && sent == 0, "preparation did not cancel before first key")
            print("CANCEL preparation latency_ms=\(fixed((ProcessInfo.processInfo.systemUptime - start) * 1000, 3)); posts=0")
        }
        test("Escape reaches calibration recording through the key-equivalent path") {
            let owner = CalibrationWindow(current: Settings(), directory: directory.appendingPathComponent("escape")); defer { owner.cancel() }
            let event = key(27, timestamp: 1000, code: 53)
            try require(owner.window!.performKeyEquivalent(with: event), "Escape was not consumed by capture window")
            try require(owner.session != nil && !owner.session!.finished && owner.session!.ignoredKeys == 1, "Escape cancelled recording or was not counted")
            try require(!owner.length.isEnabled && owner.session!.raw.contains { $0.contains("ignored\tcharacter 27") }, "Escape did not start clock and record character 27")
            try equal(owner.cancelButton.keyEquivalent, "", "Escape is still a cancel shortcut")
        }
        test("calibration and results block owner close and centre over the main window") {
            let main = try MainWindow(directory: directory.appendingPathComponent("modal"), diagnostic: true)
            defer { main.shutdown(); main.window?.close() }
            main.window!.setFrameOrigin(NSPoint(x: 100, y: 100)); main.openCalibration()
            for child in [main.calibration!.window!] {
                try require(main.modalChild === child && !main.windowShouldClose(main.window!), "calibration did not block owner")
                try require(abs(child.frame.midX - main.window!.frame.midX) < 1 && abs(child.frame.midY - main.window!.frame.midY) < 1, "capture not centred")
            }
            let d = main.calibration!; d.input.keyDown(with: key(83, timestamp: 2000)); d.finish()
            guard let result = main.results else { throw TestFailure(description: "no modal results") }
            try require(main.modalChild === result.window && !main.windowShouldClose(main.window!), "results did not block owner")
            try require(abs(result.window!.frame.midX - main.window!.frame.midX) < 1 && abs(result.window!.frame.midY - main.window!.frame.midY) < 1, "results not centred")
            result.discard(); try require(main.windowShouldClose(main.window!) && main.modalChild == nil, "discard did not release owner")
        }
        test("Edit menu shortcuts are consumed and counted by calibration input") {
            let d = CalibrationWindow(current: Settings(), directory: directory.appendingPathComponent("shortcuts")); defer { d.cancel() }
            let menu = applicationMenu(target: nil) as! CaptureAwareMenu; menu.captureInput = { d.input }
            for c in units("zvxca") { try require(menu.performKeyEquivalent(with: key(c, timestamp: 1000, modifiers: .command)), "menu consumed shortcut before capture") }
            try equal(d.session!.ignoredKeys, 5, "shortcut count")
            try require(d.session!.keys.isEmpty && d.input.string.isEmpty, "shortcut inserted or removed text")
            let mouse = NSEvent.mouseEvent(with: .rightMouseDown, location: .zero, modifierFlags: [], timestamp: 1001, windowNumber: 0, context: nil, eventNumber: 1, clickCount: 1, pressure: 0)!
            d.input.rightMouseDown(with: mouse)
            try require(d.session!.raw.last!.hasSuffix("ignored\tright-click"), "right click reused double-click reason")
        }
        test("settings scroll affordance is visible and opens at top in both appearances") {
            let main = try MainWindow(directory: directory.appendingPathComponent("scroll"), diagnostic: true); defer { main.shutdown(); main.window?.close() }
            try require(main.settingsScroll.hasVerticalScroller && !main.settingsScroll.autohidesScrollers && main.settingsScroll.scrollerStyle == .legacy && !main.scrollHint.isHidden, "settings scrolling is hidden")
            try require(main.settingsScroll.documentView!.visibleRect.maxY >= main.settingsScroll.documentView!.bounds.maxY - 1, "settings did not open at top")
            for (name, appearance) in [("light",NSAppearance.Name.aqua),("dark",.darkAqua)] { try renderWindow(main.window!, evidence.appendingPathComponent("settings-scroll-\(name).png"), appearance: appearance) }
            let document = main.settingsScroll.documentView!, last = main.controls["CorrectionResumeMilliseconds"]!
            try require(document.bounds.height > main.settingsScroll.contentView.bounds.height, "settings document has no scrollable extent")
            _ = document.scrollToVisible(last.frame)
            try require(document.visibleRect.contains(last.frame), "last Correction row cannot be scrolled into view")
            for (name, appearance) in [("light",NSAppearance.Name.aqua),("dark",.darkAqua)] { try renderWindow(main.window!, evidence.appendingPathComponent("settings-correction-\(name).png"), appearance: appearance) }
        }
        test("numeric edits clamp quietly when editing ends") {
            let main = try MainWindow(directory: directory.appendingPathComponent("numeric"), diagnostic: true); defer { main.shutdown(); main.window?.close() }
            for control in main.orderedControls where !control.spec.isBool {
                let field = control.field!
                try require((field.formatter as! NumberFormatter).minimum == nil && (field.formatter as! NumberFormatter).maximum == nil, "formatter traps out-of-range editing")
                for text in ["999", "-5"] {
                    field.stringValue = text
                    control.controlTextDidEndEditing(Notification(name: NSControl.textDidEndEditingNotification, object: field))
                    let expected = control.spec.clamp(text == "999" ? (control.spec.isMultiplier ? 99900 : 999) : -5)
                    try equal(control.value, expected, "clamp \(control.spec.name)")
                    try equal(field.doubleValue, Double(expected) / (control.spec.isMultiplier ? 100 : 1), "display after clamp")
                }
            }
        }
        test("shutdown saves edited settings on quit") {
            let data = directory.appendingPathComponent("quit")
            let main = try MainWindow(directory: data, diagnostic: true)
            main.controls["WordsPerMinute"]!.value = 137; main.controls["ButtonDelaySeconds"]!.value = 9
            let expected = main.currentSettings()
            let delegate = ApplicationDelegate(); delegate.main = main
            delegate.applicationWillTerminate(Notification(name: NSApplication.willTerminateNotification))
            try equal(try Settings.load(directory: data), expected, "quit did not save settings")
            main.window?.close()
        }
        test("all ten correction-dependent controls follow Corrected mistakes") {
            let main = try MainWindow(directory: directory.appendingPathComponent("dependent"), diagnostic: true); defer { main.shutdown(); main.window?.close() }
            let names = ["TypoChancePercent", "DuplicateChancePercent", "TransposeChancePercent", "CorrectionNoticeMilliseconds", "CorrectionDetectChars", "CorrectionPauseMilliseconds", "CorrectionResumeMilliseconds", "NumberTypoMultiplierPercent", "PunctuationTypoMultiplierPercent", "ShiftSymbolTypoMultiplierPercent"]
            for value in [0,1,0,1] {
                main.controls["EnableTypos"]!.value = value; main.controls["EnableTypos"]!.valueChanged()
                for name in names { let c = main.controls[name]!; try equal(c.enabled, value != 0, "dependent \(name)"); try equal(c.field!.isEnabled, value != 0, "field \(name)"); try equal(c.stepper!.isEnabled, value != 0, "stepper \(name)") }
            }
        }
        test("test script uses temporary product evidence by default and honours its override") {
            let root = directory.appendingPathComponent("script-default"), mac = root.appendingPathComponent("mac"), bin = root.appendingPathComponent("bin"), temporary = root.appendingPathComponent("temporary")
            for folder in [mac,bin,temporary] { try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true) }
            let source = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent()
            let script = mac.appendingPathComponent("test.sh")
            try FileManager.default.copyItem(at: source.appendingPathComponent("test.sh"), to: script)
            // Only compilation is replaced: exercise the real script's directory
            // creation, environment, evidence selection, output and cleanup.
            let compiler = bin.appendingPathComponent("swiftc")
            let fixture = """
            #!/bin/bash
            set -eu
            target=''
            while [[ "$#" -gt 0 ]]; do
              if [[ "$1" == -o ]]; then target="$2"; shift 2; else shift; fi
            done
            printf '%s\\n' "$target" > "$AUTOWRITE_SCRIPT_BINARY"
            cat > "$target" <<'PROGRAM'
            #!/bin/bash
            printf '%s\\n' "$2" > "$AUTOWRITE_SCRIPT_RESULT"
            printf 'TOTAL: 1 PASS, 0 FAIL; stand-in\\n'
            PROGRAM
            chmod 755 "$target"
            """
            try fixture.write(to: compiler, atomically: true, encoding: .utf8)
            try FileManager.default.setAttributes([.posixPermissions:0o755], ofItemAtPath: compiler.path)
            let result = root.appendingPathComponent("result"), binary = root.appendingPathComponent("binary")
            for useOverride in [false,true] {
                let process = Process(), output = Pipe()
                var env = ProcessInfo.processInfo.environment
                env["PATH"] = bin.path + ":" + (env["PATH"] ?? "/usr/bin:/bin")
                env["TMPDIR"] = temporary.path; env["AUTOWRITE_SCRIPT_RESULT"] = result.path; env["AUTOWRITE_SCRIPT_BINARY"] = binary.path
                let custom = root.appendingPathComponent("chosen-evidence")
                if useOverride { env["AUTOWRITE_EVIDENCE_DIR"] = custom.path } else { env.removeValue(forKey: "AUTOWRITE_EVIDENCE_DIR") }
                process.environment = env; process.executableURL = URL(fileURLWithPath: "/bin/bash"); process.arguments = [script.path]
                process.standardOutput = output; process.standardError = output
                try process.run(); let printed = String(decoding: output.fileHandleForReading.readDataToEndOfFile(), as: UTF8.self); process.waitUntilExit()
                try equal(process.terminationStatus, 0, "test script fixture: " + printed)
                let path = trim(try String(contentsOf: result, encoding: .utf8)), build = trim(try String(contentsOf: binary, encoding: .utf8))
                if useOverride { try equal(path, custom.path, "evidence override") }
                else { try require(URL(fileURLWithPath: path).lastPathComponent.hasPrefix("AutoWrite-evidence.") && URL(fileURLWithPath: path).deletingLastPathComponent().path == temporary.path, "evidence is not a product temporary directory") }
                try require(printed.contains("Evidence: " + path), "evidence location not printed")
                try require(URL(fileURLWithPath: build).deletingLastPathComponent().lastPathComponent.hasPrefix("AutoWrite-tests."), "build temporary name is not product based")
                try require(!FileManager.default.fileExists(atPath: URL(fileURLWithPath: build).deletingLastPathComponent().path), "test work directory not cleaned")
            }
        }
        test("test script fails a run that stops early or reports failures, and passes the binary's exit code through") {
            let root = directory.appendingPathComponent("script-totals"), mac = root.appendingPathComponent("mac"), bin = root.appendingPathComponent("bin"), temporary = root.appendingPathComponent("temporary")
            for folder in [mac,bin,temporary] { try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true) }
            let source = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent()
            let script = mac.appendingPathComponent("test.sh")
            try FileManager.default.copyItem(at: source.appendingPathComponent("test.sh"), to: script)
            // The stand-in test binary runs whatever AUTOWRITE_STANDIN_BODY says.
            let compiler = bin.appendingPathComponent("swiftc")
            let fixture = """
            #!/bin/bash
            set -eu
            target=''
            while [[ "$#" -gt 0 ]]; do
              if [[ "$1" == -o ]]; then target="$2"; shift 2; else shift; fi
            done
            printf '#!/bin/bash\\n%s\\n' "$AUTOWRITE_STANDIN_BODY" > "$target"
            chmod 755 "$target"
            """
            try fixture.write(to: compiler, atomically: true, encoding: .utf8)
            try FileManager.default.setAttributes([.posixPermissions:0o755], ofItemAtPath: compiler.path)
            let early = "FAIL test run stopped before its TOTAL line", failed = "FAIL test run reported failures in its TOTAL line"
            let cases: [(String, Int32, String)] = [
                ("printf 'PASS one\\n'; exit 0", 1, early + " (exit code 0)"),
                ("printf 'PASS one\\nTOTAL: 1 PASS, 2 FAIL; stand-in\\n'; exit 0", 1, failed + " (exit code 0)"),
                ("printf 'PASS one\\n'; exit 7", 7, early + " (exit code 7)"),
                ("printf 'PASS one\\nTOTAL: 1 PASS, 0 FAIL; stand-in\\n'; exit 0", 0, "TOTAL: 1 PASS, 0 FAIL; stand-in"),
            ]
            for (body, expected, last) in cases {
                let process = Process(), output = Pipe()
                var env = ProcessInfo.processInfo.environment
                env["PATH"] = bin.path + ":" + (env["PATH"] ?? "/usr/bin:/bin")
                env["TMPDIR"] = temporary.path; env["AUTOWRITE_STANDIN_BODY"] = body
                env["AUTOWRITE_EVIDENCE_DIR"] = root.appendingPathComponent("evidence").path
                process.environment = env; process.executableURL = URL(fileURLWithPath: "/bin/bash"); process.arguments = [script.path]
                process.standardOutput = output; process.standardError = output
                try process.run(); let printed = String(decoding: output.fileHandleForReading.readDataToEndOfFile(), as: UTF8.self); process.waitUntilExit()
                try equal(process.terminationStatus, expected, "test script exit for: " + body + "\n" + printed)
                try require(printed.contains("PASS one\n"), "per-test output not passed through: " + printed)
                try equal(printed.split(separator: "\n").last.map(String.init) ?? "", last, "test script last line for: " + body)
            }
        }
        test("smoke script rejects bad arguments and missing builds without launching") {
            let root = directory.appendingPathComponent("smoke-refusal"), folder = root.appendingPathComponent("mac")
            try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
            let source = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().appendingPathComponent("smoke.sh")
            let script = folder.appendingPathComponent("smoke.sh"); try FileManager.default.copyItem(at: source, to: script)
            for (arguments, expected) in [([String](),Int32(4)),(["--bogus"],4),(["--live-typing","--require-idle-seconds","239"],4),(["--live-typing"],2),(["--live-typing","--require-idle-seconds","240"],2)] {
                let process = Process(), output = Pipe()
                process.executableURL = URL(fileURLWithPath: "/bin/bash"); process.arguments = [script.path] + arguments
                process.standardOutput = output; process.standardError = output
                try process.run(); _ = output.fileHandleForReading.readDataToEndOfFile(); process.waitUntilExit()
                try equal(process.terminationStatus, expected, "smoke refusal")
            }
        }
        test("owner proof arguments and refusal paths never launch or request real permission") {
            try equal(LiveTypingProbe.parse(["--live-typing"]), .owner, "owner syntax")
            try equal(LiveTypingProbe.parse(["--live-typing", "--require-idle-seconds", "240"]), .automation(240), "automation syntax")
            let work = directory.appendingPathComponent("owner-refusal"); try FileManager.default.createDirectory(at: work, withIntermediateDirectories: true)
            let args = ["--live-typing","--owner-proof-directory",work.path]
            let permission = AccessibilityPermission(trusted: { false }, canPost: { false }, requestListing: {})
            var explanations = 0, proofs = 0
            func run() -> Int32 { LiveTypingProbe.run(arguments: args, permission: permission, gateRead: { LiveGate(idle: 0, onConsole: true, unlocked: true) }, ownerExplanation: { _, _ in explanations += 1 }, proof: { _, _ in proofs += 1; return 0 }, runningCopies: { [] }) }
            try equal(run(), 2, "unprepared owner launch")
            try equal(explanations, 0, "unprepared owner prompted")
            try String(Date().timeIntervalSince1970 - 1).write(to: work.appendingPathComponent("deadline"), atomically: true, encoding: .utf8)
            try equal(run(), 2, "expired prepared owner launch")
            try equal(explanations, 0, "expired owner launch reached permission explanation")
            try equal(proofs, 0, "expired owner launch reached proof")
            try String(Date().timeIntervalSince1970 + 15).write(to: work.appendingPathComponent("deadline"), atomically: true, encoding: .utf8)
            try equal(run(), 2, "owner denied permission")
            try equal(explanations, 1, "owner permission refusal missing explanation")
            try equal(proofs, 0, "owner refusal launched proof")
            permission.trusted = { true }; permission.canPost = { true }
            try equal(run(), 0, "owner zero-idle preflight did not reach injected proof")
            try equal(proofs, 1, "injected owner proof not called")
            let blocked = LiveTypingProbe.run(arguments: args, permission: permission, gateRead: { LiveGate(idle: 1000, onConsole: true, unlocked: false) }, ownerExplanation: { _, _ in explanations += 1 }, proof: { _, _ in proofs += 1; return 0 }, runningCopies: { [] })
            try equal(blocked, 3, "owner lock refusal")
            try equal(proofs, 1, "locked owner launched proof")
        }
    }
}

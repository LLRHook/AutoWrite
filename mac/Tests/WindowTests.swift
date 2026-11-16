import AppKit

func renderWindow(_ window: NSWindow, _ path: URL, appearance: NSAppearance.Name) throws {
    window.appearance = NSAppearance(named: appearance)
    let view = window.contentView!
    view.layoutSubtreeIfNeeded(); view.displayIfNeeded()
    guard let bitmap = view.bitmapImageRepForCachingDisplay(in: view.bounds) else { throw TestFailure(description: "cannot allocate window bitmap") }
    view.cacheDisplay(in: view.bounds, to: bitmap)
    guard let png = bitmap.representation(using: .png, properties: [:]) else { throw TestFailure(description: "cannot encode PNG") }
    try png.write(to: path)
    try require(!window.isVisible, "diagnostic window was shown")
}
func key(_ character: Unit, timestamp: Double, code: UInt16 = 0, modifiers: NSEvent.ModifierFlags = [], repeatKey: Bool = false) -> NSEvent {
    NSEvent.keyEvent(with: .keyDown, location: .zero, modifierFlags: modifiers, timestamp: timestamp, windowNumber: 0, context: nil, characters: string([character]), charactersIgnoringModifiers: string([character]), isARepeat: repeatKey, keyCode: code)!
}

extension TestRunner {
    func windows(_ f: Fixtures, _ directory: URL, _ evidence: URL) throws {
        let app = NSApplication.shared
        app.setActivationPolicy(.prohibited)
        let original = NSWorkspace.shared.frontmostApplication?.processIdentifier
        let mainData = directory.appendingPathComponent("main")
        let current = Settings(); try require(current.save(directory: mainData), "initial settings save")
        let permission = AccessibilityPermission(trusted: { false }, canPost: { false }, requestListing: {})
        let main = try MainWindow(directory: mainData, permission: permission, diagnostic: true)
        defer { main.shutdown(); main.window?.close() }
        test("real main window: all 26 controls, ranges, defaults, labels, order and steps") {
            let ranges = try f.rows("settings-ranges.tsv")
            for row in ranges {
                let c = main.controls[row[0]]!
                try equal(c.spec.label, row[1], "label")
                try equal(c.value, current[row[0]], "default")
                if !c.spec.isBool {
                    let scale = c.spec.isMultiplier ? 100.0 : 1.0
                    try equal(c.stepper!.minValue, Double(row[2])! / scale, "minimum")
                    try equal(c.stepper!.maxValue, Double(row[3])! / scale, "maximum")
                    try equal(c.stepper!.increment, c.spec.isMultiplier ? 0.25 : 1, "step")
                    let formatter = c.field!.formatter as! NumberFormatter
                    try equal(formatter.minimumFractionDigits, c.spec.isMultiplier ? 2 : 0, "decimal places")
                    try equal(formatter.maximumFractionDigits, c.spec.isMultiplier ? 2 : 0, "max decimals")
                }
            }
            let names = main.orderedControls.map { $0.spec.name }
            try equal(names, ["WordsPerMinute", "ButtonDelaySeconds"] + Settings.specs.map { $0.name }.filter { $0 != "WordsPerMinute" && $0 != "ButtonDelaySeconds" }, "UI order")
            try equal(main.sectionLabels, ["Typing", "Rhythm", "Difficult keys", "Correction"], "sections")
            try equal(main.currentSettings(), current, "initial loaded values")
        }
        test("real controls: delay label, steppers, correction toggle and running/calibration locks") {
            main.controls["ButtonDelaySeconds"]!.stepper!.integerValue = 7; main.controls["ButtonDelaySeconds"]!.didStep()
            try equal(main.startButton.title, "Type in 7s", "button follows delay")
            main.controls["EnableTypos"]!.checkbox!.performClick(nil)
            try require(!main.controls["TypoChancePercent"]!.enabled && !main.controls["NumberTypoMultiplierPercent"]!.enabled, "correction controls still enabled")
            try require(main.controls["WordsPerMinute"]!.enabled, "unrelated control disabled")
            main.running = true
            try require(main.controls.values.allSatisfy { !$0.enabled } && !main.text.isEditable && !main.startButton.isEnabled && !main.calibrateButton.isEnabled && main.stopButton.isEnabled, "typing lock")
            main.running = false; main.calibrationOpen = true
            try require(main.controls.values.allSatisfy { !$0.enabled } && !main.startButton.isEnabled && !main.stopButton.isEnabled, "calibration lock")
            main.calibrationOpen = false; main.setSettings(current)
        }
        test("main and calibration editors preserve literal text and standard Edit menu") {
            let text = main.text
            try require(!text.isRichText && !text.isAutomaticQuoteSubstitutionEnabled && !text.isAutomaticDashSubstitutionEnabled && !text.isAutomaticTextReplacementEnabled && !text.isAutomaticSpellingCorrectionEnabled && !text.isAutomaticTextCompletionEnabled && !text.smartInsertDeleteEnabled, "main substitutions enabled")
            let pasted = "aB \"quotes\" -- aren't... café 😀\r\n\t\u{201C}exact\u{201D}"
            text.insertText(pasted, replacementRange: NSRange(location: 0, length: 0))
            try equal(units(text.string), units(pasted), "literal inserted text")
            let menu = applicationMenu(target: nil)
            try equal(menu.items[1].submenu!.items.map { $0.action!.description }, ["undo:", "redo:", "cut:", "copy:", "paste:", "selectAll:"], "Edit actions")
            try require(text.allowsUndo, "Undo is disabled")
        }
        test("permission states: denial, grant, rebuild revocation, restart message; no posts") {
            main.text.string = "q"
            main.requestTyping(buttonDelay: false)
            try require(main.status.stringValue.contains("Accessibility") && !main.running, "missing permission silent")
            permission.trusted = { true }; permission.canPost = { true }; main.requestTyping(buttonDelay: false)
            try equal(main.status.stringValue, "Accessibility permission granted. Ready.", "permission after grant")
            permission.trusted = { false }; main.requestTyping(buttonDelay: true)
            try require(main.status.stringValue.contains("After rebuilding") && !main.running, "revoked permission ignored")
            permission.trusted = { true }; permission.canPost = { false }; main.requestTyping(buttonDelay: false)
            try require(main.status.stringValue.contains("Quit and reopen"), "restart advice missing")
            let alert = PermissionWindow(permission: permission, state: .missing)
            try renderWindow(alert.window!, evidence.appendingPathComponent("permission-light.png"), appearance: .aqua)
            try renderWindow(alert.window!, evidence.appendingPathComponent("permission-dark.png"), appearance: .darkAqua)
            alert.cancel()
        }
        test("real Start/Stop buttons: delay, progress, status and runtime injection without posts") {
            permission.trusted = { true }; permission.canPost = { true }; main.text.string = "abc"
            main.controls["ButtonDelaySeconds"]!.value = 7; main.updateControlState()
            let virtual = VirtualRuntime(seed: 17)
            main.runtimeFactory = { _ in virtual }
            main.startButton.performClick(nil)
            try require(main.running && !main.startButton.isEnabled && main.stopButton.isEnabled, "start did not lock controls")
            let deadline = Date(timeIntervalSinceNow: 3)
            while main.running && Date() < deadline { RunLoop.current.run(until: Date(timeIntervalSinceNow: 0.005)) }
            try require(!main.running && main.status.stringValue.hasPrefix("Done. Log: "), "start did not finish")
            try equal(virtual.keys.first?.milliseconds, 7000, "button delay")
            try equal(main.progress.doubleValue, 100, "completion progress")
            main.startButton.performClick(nil); main.stopButton.performClick(nil)
            let stopDeadline = Date(timeIntervalSinceNow: 3)
            while main.running && Date() < stopDeadline { RunLoop.current.run(until: Date(timeIntervalSinceNow: 0.005)) }
            try require(!main.running && main.status.stringValue == "Stopped", "Stop did not finish")
            main.runtimeFactory = nil; main.setSettings(current)
        }
        test("real calibration input: event times, Backspace, Enter, ignored edits and key repeat") {
            let d = CalibrationWindow(current: current, directory: directory)
            defer { d.cancel() }
            try equal(d.passage.string, string(calibrationPassage), "visible passage text")
            try equal(d.length.indexOfSelectedItem, 1, "60 seconds default")
            try equal(d.clock.stringValue, "Clock starts with your first character", "initial clock")
            try require(!d.input.isRichText && !d.input.isAutomaticQuoteSubstitutionEnabled && !d.input.isAutomaticDashSubstitutionEnabled && !d.input.isAutomaticSpellingCorrectionEnabled, "capture substitutions enabled")
            d.length.selectItem(at: 0)
            let shift = NSEvent.keyEvent(with: .flagsChanged, location: .zero, modifierFlags: .shift, timestamp: 999, windowNumber: 0, context: nil, characters: "", charactersIgnoringModifiers: "", isARepeat: false, keyCode: 56)!
            d.input.flagsChanged(with: shift)
            try require(d.session!.keys.isEmpty && !d.session!.started && d.session!.raw.last == "0.000\tkeydown\t16", "modifier changed input or started clock")
            d.input.keyDown(with: key(83, timestamp: 1000))
            d.input.keyDown(with: key(120, timestamp: 1000.123))
            d.input.keyDown(with: key(8, timestamp: 1000.234, code: 51))
            d.input.keyDown(with: key(109, timestamp: 1000.345, repeatKey: true))
            d.input.keyDown(with: key(13, timestamp: 1000.456, code: 36))
            try equal(string(d.session!.typed), "Sm\n", "Backspace and Enter")
            try equal(d.input.string, "Sm\n", "visible input text")
            try equal(d.session!.seconds, 30, "length selection")
            try require(abs(d.session!.keys[1].milliseconds - 123) < 1e-8, "event time replaced by delivery time")
            try require(!d.length.isEnabled, "length editable after first character")
            let before = d.input.string
            let ignored = [key(9, timestamp: 1000.5, code: 48), key(0xF702, timestamp: 1000.6, code: 123), key(127, timestamp: 1000.7, code: 117), key(8, timestamp: 1000.8, code: 51, modifiers: .command), key(8, timestamp: 1000.9, code: 51, modifiers: .option), key(118, timestamp: 1001, modifiers: .command)]
            for e in ignored { d.input.keyDown(with: e) }
            d.input.paste(nil); d.input.cut(nil); d.input.delete(nil); d.input.selectAll(nil)
            let mouse = NSEvent.mouseEvent(with: .leftMouseDown, location: .zero, modifierFlags: [], timestamp: 1001.1, windowNumber: 0, context: nil, eventNumber: 1, clickCount: 1, pressure: 0)!
            d.input.mouseDown(with: mouse)
            try equal(d.input.string, before, "ignored edits changed input")
            try equal(d.session!.ignoredKeys, 11, "ignored count")
            d.refreshProgress(); try require(d.marker > 0, "passage marker never moved")
            d.tick(1001.2)
            d.window!.contentView!.layoutSubtreeIfNeeded()
            let passageLayout = d.passage.layoutManager!
            passageLayout.ensureLayout(for: d.passage.textContainer!)
            try require(passageLayout.usedRect(for: d.passage.textContainer!).height > 0, "passage has no laid-out text")
            let geometry = "input frame=\(d.input.frame); visible=\(d.input.visibleRect); bounds=\(d.input.bounds); glyphs=\(d.input.layoutManager?.numberOfGlyphs ?? 0); used=\(d.input.layoutManager!.usedRect(for: d.input.textContainer!)); length=\(d.input.string.utf16.count)"
            try geometry.write(to: evidence.appendingPathComponent("capture-geometry.txt"), atomically: true, encoding: .utf8)
            try renderWindow(d.window!, evidence.appendingPathComponent("calibration-light.png"), appearance: .aqua)
            try renderWindow(d.window!, evidence.appendingPathComponent("calibration-dark.png"), appearance: .darkAqua)
        }
        test("real calibration flow: simulated typist, results rows, Apply saves immediately") {
            main.openCalibration()
            guard let capture = main.calibration else { throw TestFailure(description: "calibration did not open") }
            capture.length.selectItem(at: 0)
            for (i, c) in calibrationPassage.prefix(250).enumerated() { capture.input.keyDown(with: key(c, timestamp: 2000 + Double(i) * 0.1, code: c == 10 ? 36 : 0)) }
            capture.refreshProgress(); try equal(capture.marker, 250, "passage position")
            capture.tick(2030)
            try require(main.calibration == nil && main.results != nil && main.calibrationOpen, "result sequence")
            guard let results = main.results else { throw TestFailure(description: "results did not open") }
            try equal(results.rows.count, 26, "result rows")
            try equal(results.summary.stringValue, results.result.summary, "summary")
            for (row, estimate) in zip(results.rows, results.result.estimates) {
                try equal(row.setting, estimate.setting, "row setting")
                try equal(row.current.stringValue, current.formatted(estimate.setting), "current")
                try equal(row.proposed.stringValue, results.result.proposed.formatted(estimate.setting), "proposed")
                try equal(row.reason.stringValue, estimate.reason, "reason")
            }
            try renderWindow(results.window!, evidence.appendingPathComponent("results-light.png"), appearance: .aqua)
            try renderWindow(results.window!, evidence.appendingPathComponent("results-dark.png"), appearance: .darkAqua)
            let proposed = results.result.proposed
            try require(!results.rows.isEmpty, "empty results")
            results.applyButton.performClick(nil)
            try equal(main.currentSettings(), proposed, "applied controls")
            try equal(try Settings.load(directory: mainData), proposed, "saved Apply")
            try require(!main.calibrationOpen && main.results == nil && main.startButton.isEnabled, "Apply unlock")
            let captures = try FileManager.default.contentsOfDirectory(at: mainData.appendingPathComponent("calibration"), includingPropertiesForKeys: nil)
            try equal(captures.count, 1, "recording count")
            let saved = try String(contentsOf: captures[0], encoding: .utf8)
            try require(saved.contains("completed=True") && saved.contains("# setting\tWordsPerMinute\t80") && saved.contains("\tkeydown\t"), "capture metadata or raw keys missing")
        }
        test("Discard and Cancel leave settings unchanged; Apply failure restores controls") {
            let before = main.currentSettings(), saved = try String(contentsOf: mainData.appendingPathComponent("settings.ini"), encoding: .utf8)
            main.openCalibration(); main.calibration!.input.keyDown(with: key(83, timestamp: 3000)); main.calibration!.cancelButton.performClick(nil)
            try require(!main.calibrationOpen && main.results == nil, "Cancel did not unlock")
            main.openCalibration(); let capture = main.calibration!
            for (i, c) in calibrationPassage.prefix(60).enumerated() { capture.input.keyDown(with: key(c, timestamp: 4000 + Double(i) * 0.1)) }
            capture.finish(); main.results!.discardButton.performClick(nil)
            try equal(main.currentSettings(), before, "Discard changed controls")
            try equal(try String(contentsOf: mainData.appendingPathComponent("settings.ini"), encoding: .utf8), saved, "Discard/Cancel saved")
            var proposed = before; proposed["WordsPerMinute"] = before["WordsPerMinute"] == 100 ? 90 : 100
            main.save = { _ in false }
            let result = CalibrationResult(current: proposed), results = ResultsWindow(result: result, apply: main.applyCalibration)
            results.applyButton.performClick(nil)
            try equal(main.currentSettings(), before, "failed save did not restore")
            try require(results.warning.stringValue.contains("Controls were restored"), "failed save warning missing")
            main.save = nil; results.discard()
        }
        test("recording save failure still shows results and releases controls") {
            let bad = directory.appendingPathComponent("blocked-recording")
            try FileManager.default.createDirectory(at: bad, withIntermediateDirectories: true)
            try Data().write(to: bad.appendingPathComponent("calibration"))
            let owner = try MainWindow(directory: bad, diagnostic: true)
            defer { owner.shutdown(); owner.window?.close() }
            owner.openCalibration()
            guard let d = owner.calibration else { throw TestFailure(description: "capture did not open") }
            d.input.keyDown(with: key(83, timestamp: 5000)); d.finish()
            guard let result = owner.results else { throw TestFailure(description: "recording error prevents results") }
            try require(result.warning.stringValue.hasPrefix("The recording could not be saved. You can still review and apply these settings."), "recording save warning missing")
            result.applyButton.performClick(nil)
            try require(!owner.calibrationOpen && owner.startButton.isEnabled, "save failure did not unlock controls")
            try equal(try Settings.load(directory: bad), owner.currentSettings(), "Apply after recording save failure")
        }
        test("main window light/dark offscreen rendering; no focus changes") {
            main.setSettings(Settings()); main.setStatus("Ready"); main.text.string = "Paste text here, then focus another app and press Fn+F7.\n\nAutoWrite preserves your text: café, \"quotes\", symbols and emoji 😀."
            for (name, appearance) in [("light", NSAppearance.Name.aqua), ("dark", NSAppearance.Name.darkAqua)] { try renderWindow(main.window!, evidence.appendingPathComponent("main-\(name).png"), appearance: appearance) }
            try equal(NSWorkspace.shared.frontmostApplication?.processIdentifier, original, "foreground changed")
            try require(app.windows.allSatisfy { !$0.isVisible }, "test exposed a window")
        }
    }
}

import AppKit

final class CaptureInputView: LiteralTextView {
    var press: ((Int, Double) -> Void)?
    var character: ((Unit, Double) -> Void)?
    var ignored: ((String, Double) -> Void)?
    override init(frame: NSRect = .zero) {
        super.init(frame: frame); allowsUndo = false
        identifier = NSUserInterfaceItemIdentifier("CalibrationInput"); setAccessibilityLabel("Typing input")
    }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    override func keyDown(with event: NSEvent) {
        let key = Int(event.keyCode), time = event.timestamp
        let chars = Array((event.characters ?? "").utf16)
        // Record the key's hardware timestamp, not delivery time. No text input
        // interpretation is invoked: key repeats cannot open the accent chooser.
        press?(Self.windowsKey(key, chars), time)
        let mods = event.modifierFlags.intersection([.command, .control])
        if key == 51 && !event.modifierFlags.intersection([.command, .control, .option]).isEmpty { ignored?(event.modifierFlags.contains(.command) ? "Command+Backspace" : event.modifierFlags.contains(.option) ? "Option+Backspace" : "Ctrl+Backspace", time); return }
        let navigation = [48:"Tab", 123:"Left", 124:"Right", 126:"Up", 125:"Down", 115:"Home", 119:"End", 117:"Delete", 116:"PageUp", 121:"PageDown"]
        if let name = navigation[key] { ignored?("key " + name, time); return }
        if !mods.isEmpty { ignored?("key shortcut", time); return }
        if key == 51 { character?(8, time); return }
        if key == 36 || key == 76 { character?(13, time); return }
        for c in chars {
            if c == 9 || c == 127 { ignored?("shortcut character \(c)", time) }
            else { character?(c, time) }
        }
    }
    override func flagsChanged(with event: NSEvent) {
        let modifiers: [UInt16: (NSEvent.ModifierFlags, Int)] = [56:(.shift, 16), 60:(.shift, 16), 59:(.control, 17), 62:(.control, 17), 58:(.option, 18), 61:(.option, 18), 55:(.command, 91), 54:(.command, 92), 63:(.function, 255)]
        if let (flag, code) = modifiers[event.keyCode], event.modifierFlags.contains(flag) { press?(code, event.timestamp) }
    }
    static func windowsKey(_ key: Int, _ chars: [Unit]) -> Int {
        let map = [51:8, 36:13, 76:13, 48:9, 123:37, 124:39, 126:38, 125:40, 115:36, 119:35, 117:46, 116:33, 121:34]
        if let code = map[key] { return code }
        if let c = chars.first { return Int(DotChar.toUpper(c)) }
        return key
    }
    private func ignoreEdit(_ reason: String) { ignored?(reason, ProcessInfo.processInfo.systemUptime) }
    override func paste(_ sender: Any?) { ignoreEdit("edit message 770") }
    override func cut(_ sender: Any?) { ignoreEdit("edit message 768") }
    override func copy(_ sender: Any?) { ignoreEdit("key shortcut") }
    override func selectAll(_ sender: Any?) { ignoreEdit("key shortcut") }
    override func delete(_ sender: Any?) { ignoreEdit("edit message 771") }
    override func mouseDown(with event: NSEvent) { window?.makeFirstResponder(self); setSelectedRange(NSRange(location: (string as NSString).length, length: 0)); ignored?("edit message 513", event.timestamp) }
    override func mouseDragged(with event: NSEvent) {}
    override func rightMouseDown(with event: NSEvent) { ignored?("edit message 515", event.timestamp) }
    override func performDragOperation(_ sender: NSDraggingInfo) -> Bool { ignoreEdit("edit message 770"); return false }
    override func insertText(_ insertString: Any, replacementRange: NSRange) { ignoreEdit("edit message 770") }
}

final class CalibrationWindow: NSWindowController, NSWindowDelegate {
    let current: Settings, directory: URL
    let length = NSPopUpButton(), clock = label("Clock starts with your first character")
    let passage = LiteralTextView(), input = CaptureInputView()
    var cancelButton: NSButton!
    private(set) var session: CaptureSession?
    private(set) var marker = 0, progressUpdates = 0
    private var firstTime: Double?, timer: Timer?, progressDirty = false, ending = false
    var completed: ((CaptureSession?, String) -> Void)?
    init(current: Settings, directory: URL) {
        self.current = current; self.directory = directory
        let w = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 840, height: 660), styleMask: [.titled, .closable, .resizable], backing: .buffered, defer: false)
        w.title = "Typing calibration"; w.minSize = NSSize(width: 700, height: 560); w.isReleasedWhenClosed = false
        super.init(window: w); w.delegate = self; w.contentView = BackgroundView()
        length.addItems(withTitles: ["30 seconds", "60 seconds"]); length.selectItem(at: 1)
        length.identifier = NSUserInterfaceItemIdentifier("CalibrationLength")
        let top = stack([length, clock], orientation: .horizontal)
        let instructions = label("Copy the passage below. Corrections are Backspace only; paste, navigation and Tab are ignored.")
        configurePlainText(passage); passage.string = string(calibrationPassage); passage.isEditable = false; passage.isSelectable = false
        passage.identifier = NSUserInterfaceItemIdentifier("CalibrationPassage"); passage.setAccessibilityLabel("Calibration passage")
        let passageScroll = textScroll(passage), inputScroll = textScroll(input)
        cancelButton = button("Cancel", target: self, action: #selector(cancel)); cancelButton.keyEquivalent = "\u{1B}"
        let bottom = stack([NSView(), cancelButton], orientation: .horizontal)
        let root = stack([top, instructions, passageScroll, inputScroll, bottom], spacing: 12); root.alignment = .width
        fill(root, in: w.contentView!, inset: 16)
        top.heightAnchor.constraint(equalToConstant: 30).isActive = true
        instructions.heightAnchor.constraint(equalToConstant: 38).isActive = true
        bottom.heightAnchor.constraint(equalToConstant: 32).isActive = true
        passageScroll.heightAnchor.constraint(equalTo: inputScroll.heightAnchor, multiplier: 1.22).isActive = true
        input.press = { [weak self] key, timestamp in
            guard let self, !self.ending else { return }; self.ensureSession()
            self.session!.raw.append(fixed(self.milliseconds(timestamp), 3) + "\tkeydown\t\(key)")
        }
        input.character = { [weak self] c, timestamp in self?.accept(c, timestamp) }
        input.ignored = { [weak self] reason, timestamp in guard let self, !self.ending else { return }; self.ensureSession(); self.session!.ignore(reason, self.milliseconds(timestamp)) }
        timer = Timer.scheduledTimer(withTimeInterval: 0.02, repeats: true) { [weak self] _ in self?.tick(ProcessInfo.processInfo.systemUptime) }
    }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    func ensureSession() {
        if session == nil { session = CaptureSession(seconds: length.indexOfSelectedItem == 0 ? 30 : 60, current: current) }
        if firstTime == nil { session!.seconds = length.indexOfSelectedItem == 0 ? 30 : 60 }
    }
    func milliseconds(_ timestamp: Double) -> Double { firstTime.map { max(0, (timestamp - $0) * 1000) } ?? 0 }
    func accept(_ key: Unit, _ timestamp: Double) {
        guard !ending else { return }; ensureSession()
        if firstTime == nil { firstTime = timestamp; length.isEnabled = false }
        if session!.accept(key, milliseconds(timestamp)) {
            input.string = string(session!.typed); input.setSelectedRange(NSRange(location: session!.typed.count, length: 0)); input.scrollToEndOfDocument(nil)
            progressDirty = true
        }
        if session!.finished { finish() }
    }
    func refreshProgress() {
        guard progressDirty, let session else { return }; progressDirty = false; progressUpdates += 1
        marker = min(calibrationPassage.count, CalibrationAnalysis.align(session.typed, calibrationPassage).end)
        let all = NSRange(location: 0, length: calibrationPassage.count)
        passage.textStorage?.addAttributes([.foregroundColor:NSColor.textColor, .backgroundColor:NSColor.textBackgroundColor], range: all)
        if marker > 0 { passage.textStorage?.addAttribute(.foregroundColor, value: NSColor.secondaryLabelColor, range: NSRange(location: 0, length: marker)) }
        if marker < calibrationPassage.count {
            let at = NSRange(location: marker, length: 1)
            passage.textStorage?.addAttribute(.backgroundColor, value: NSColor.systemYellow.withAlphaComponent(0.45), range: at)
            passage.scrollRangeToVisible(at)
        }
    }
    func tick(_ timestamp: Double) {
        refreshProgress()
        if firstTime != nil, let session {
            let elapsed = milliseconds(timestamp) / 1000
            clock.stringValue = fixed(max(0, Double(session.seconds) - elapsed), 1) + " seconds remaining"
            if elapsed >= Double(session.seconds) { finish() }
        }
    }
    func finish() { guard !ending else { return }; session?.finished = true; end(completed: true) }
    @objc func cancel() { guard !ending else { return }; end(completed: false) }
    private func end(completed didComplete: Bool) {
        ending = true; timer?.invalidate(); timer = nil; refreshProgress()
        var warning = ""
        if let session { do { try session.save(completed: didComplete, directory: directory) } catch { warning = "The recording could not be saved. You can still review and apply these settings." } }
        window?.close()
        completed?(didComplete ? session : nil, warning)
    }
    func windowShouldClose(_ sender: NSWindow) -> Bool { cancel(); return true }
    deinit { timer?.invalidate() }
}

final class ResultsWindow: NSWindowController, NSWindowDelegate {
    let result: CalibrationResult
    let summary: NSTextField, warning: NSTextField
    var rows = [(setting: String, label: NSTextField, current: NSTextField, proposed: NSTextField, reason: NSTextField)]()
    var applyButton: NSButton!, discardButton: NSButton!
    var closed: (() -> Void)?
    private let apply: (Settings) -> Bool
    private var ending = false
    init(result: CalibrationResult, apply: @escaping (Settings) -> Bool) {
        self.result = result; self.apply = apply
        summary = label(result.summary); warning = label(result.warning); warning.textColor = .systemRed
        let w = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 1040, height: 780), styleMask: [.titled, .closable, .resizable], backing: .buffered, defer: false)
        w.title = "Calibration results"; w.minSize = NSSize(width: 860, height: 580); w.isReleasedWhenClosed = false
        super.init(window: w); w.delegate = self; w.contentView = BackgroundView()
        summary.identifier = NSUserInterfaceItemIdentifier("CalibrationSummary"); warning.identifier = NSUserInterfaceItemIdentifier("CalibrationWarning")
        let table = NSStackView(); table.orientation = .vertical; table.alignment = .leading; table.spacing = 10
        func addRow(_ setting: NSTextField, _ current: NSTextField, _ proposed: NSTextField, _ reason: NSTextField) {
            let row = stack([setting, current, proposed, reason], orientation: .horizontal, spacing: 12); row.alignment = .top
            setting.widthAnchor.constraint(equalToConstant: 170).isActive = true
            current.widthAnchor.constraint(equalToConstant: 64).isActive = true; proposed.widthAnchor.constraint(equalToConstant: 64).isActive = true
            table.addArrangedSubview(row); row.translatesAutoresizingMaskIntoConstraints = false; row.widthAnchor.constraint(equalTo: table.widthAnchor).isActive = true
        }
        addRow(label("Setting", bold: true), label("Current", bold: true), label("Proposed", bold: true), label("Evidence / reason", bold: true))
        for estimate in result.estimates {
            let name = estimate.setting
            var current = result.proposed; current[name] = estimate.current
            let title = label(Settings.spec(name).label), old = label(current.formatted(name)), new = label(result.proposed.formatted(name)), reason = label(estimate.reason)
            old.isSelectable = true; new.isSelectable = true; reason.isSelectable = true
            old.setAccessibilityLabel("Current " + Settings.spec(name).label); new.setAccessibilityLabel("Proposed " + Settings.spec(name).label)
            rows.append((name, title, old, new, reason)); addRow(title, old, new, reason)
        }
        let scroll = NSScrollView(); scroll.hasVerticalScroller = true; scroll.drawsBackground = false; scroll.documentView = table; table.translatesAutoresizingMaskIntoConstraints = false
        NSLayoutConstraint.activate([table.leadingAnchor.constraint(equalTo: scroll.contentView.leadingAnchor), table.topAnchor.constraint(equalTo: scroll.contentView.topAnchor), table.widthAnchor.constraint(equalTo: scroll.contentView.widthAnchor)])
        applyButton = button("Apply", target: self, action: #selector(applyChanges)); discardButton = button("Discard", target: self, action: #selector(discard)); discardButton.keyEquivalent = "\u{1B}"
        let buttons = stack([NSView(), applyButton, discardButton], orientation: .horizontal)
        let root = stack([summary, warning, scroll, buttons], spacing: 12); root.alignment = .width
        fill(root, in: w.contentView!, inset: 16)
        summary.heightAnchor.constraint(greaterThanOrEqualToConstant: 64).isActive = true
        buttons.heightAnchor.constraint(equalToConstant: 32).isActive = true
        warning.isHidden = result.warning.isEmpty
    }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    @objc func applyChanges() {
        if apply(result.proposed) { discard() }
        else { warning.stringValue = "The settings could not be saved. Controls were restored; check the data directory's write permissions."; warning.isHidden = false }
    }
    @objc func discard() { if ending { return }; ending = true; window?.close(); closed?() }
    func windowShouldClose(_ sender: NSWindow) -> Bool { discard(); return true }
}

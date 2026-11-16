import AppKit

final class BackgroundView: NSView {
    override func draw(_ dirtyRect: NSRect) { NSColor.windowBackgroundColor.setFill(); dirtyRect.fill() }
}

func label(_ text: String, bold: Bool = false) -> NSTextField {
    let field = NSTextField(wrappingLabelWithString: text)
    field.font = bold ? .boldSystemFont(ofSize: NSFont.systemFontSize) : .systemFont(ofSize: NSFont.systemFontSize)
    field.textColor = .labelColor
    return field
}
func button(_ text: String, target: AnyObject?, action: Selector) -> NSButton {
    let b = NSButton(title: text, target: target, action: action); b.bezelStyle = .rounded; return b
}
class LiteralTextView: NSTextView {
    private let ownedStorage: NSTextStorage
    override init(frame: NSRect = .zero) {
        let storage = NSTextStorage(), manager = NSLayoutManager()
        let container = NSTextContainer(containerSize: NSSize(width: 800, height: 10_000_000))
        storage.addLayoutManager(manager); manager.addTextContainer(container)
        ownedStorage = storage
        super.init(frame: frame, textContainer: container)
        configurePlainText(self)
    }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
}

func configurePlainText(_ text: NSTextView) {
    text.isRichText = false; text.importsGraphics = false; text.allowsUndo = true
    text.isAutomaticQuoteSubstitutionEnabled = false; text.isAutomaticDashSubstitutionEnabled = false
    text.isAutomaticTextReplacementEnabled = false; text.isAutomaticSpellingCorrectionEnabled = false
    text.isAutomaticTextCompletionEnabled = false; text.isAutomaticLinkDetectionEnabled = false
    text.isAutomaticDataDetectionEnabled = false; text.smartInsertDeleteEnabled = false
    text.isContinuousSpellCheckingEnabled = false; text.isGrammarCheckingEnabled = false
    text.backgroundColor = .textBackgroundColor; text.textColor = .textColor
    text.font = .monospacedSystemFont(ofSize: 13, weight: .regular)
    text.textContainerInset = NSSize(width: 10, height: 10)
    text.isHorizontallyResizable = false; text.isVerticallyResizable = true
    text.autoresizingMask = [.width]
    text.textContainer?.widthTracksTextView = true
}
final class TextScrollView: NSScrollView {
    private var sizingText = false
    override func tile() {
        super.tile()
        guard !sizingText, let text = documentView as? NSTextView, let manager = text.layoutManager, let container = text.textContainer else { return }
        sizingText = true; defer { sizingText = false }
        let size = contentSize
        text.minSize = NSSize(width: 0, height: size.height)
        text.maxSize = NSSize(width: 10_000_000, height: 10_000_000)
        text.setFrameSize(NSSize(width: size.width, height: max(size.height, text.frame.height)))
        container.heightTracksTextView = false
        container.containerSize = NSSize(width: max(1, size.width - text.textContainerInset.width * 2), height: 10_000_000)
        manager.ensureLayout(for: container)
        let height = max(size.height, manager.usedRect(for: container).height + text.textContainerInset.height * 2)
        text.setFrameSize(NSSize(width: size.width, height: height))
        manager.ensureLayout(for: container)
        text.needsDisplay = true
    }
}
func textScroll(_ text: NSTextView) -> NSScrollView {
    let scroll = TextScrollView(); scroll.hasVerticalScroller = true; scroll.borderType = .bezelBorder
    scroll.documentView = text
    return scroll
}
func stack(_ views: [NSView], orientation: NSUserInterfaceLayoutOrientation = .vertical, spacing: CGFloat = 8) -> NSStackView {
    let s = NSStackView(views: views); s.orientation = orientation; s.spacing = spacing; s.alignment = orientation == .vertical ? .leading : .centerY
    if orientation == .vertical { for view in views { view.translatesAutoresizingMaskIntoConstraints = false; view.widthAnchor.constraint(equalTo: s.widthAnchor).isActive = true } }
    return s
}
func fill(_ child: NSView, in parent: NSView, inset: CGFloat = 0) {
    child.translatesAutoresizingMaskIntoConstraints = false; parent.addSubview(child)
    NSLayoutConstraint.activate([child.leadingAnchor.constraint(equalTo: parent.leadingAnchor, constant: inset), child.trailingAnchor.constraint(equalTo: parent.trailingAnchor, constant: -inset), child.topAnchor.constraint(equalTo: parent.topAnchor, constant: inset), child.bottomAnchor.constraint(equalTo: parent.bottomAnchor, constant: -inset)])
}

final class SettingControl: NSView, NSTextFieldDelegate {
    let spec: SettingSpec
    let nameLabel: NSTextField
    let field: NSTextField?
    let stepper: NSStepper?
    let checkbox: NSButton?
    var changed: (() -> Void)?
    var enabled: Bool = true { didSet { field?.isEnabled = enabled; stepper?.isEnabled = enabled; checkbox?.isEnabled = enabled } }
    var value: Int {
        get {
            if let checkbox { return checkbox.state == .on ? 1 : 0 }
            return spec.clamp(roundEven((field?.doubleValue ?? 0) * (spec.isMultiplier ? 100 : 1)))
        }
        set {
            let value = spec.clamp(newValue)
            checkbox?.state = value != 0 ? .on : .off
            let display = Double(value) / (spec.isMultiplier ? 100 : 1)
            field?.doubleValue = display; stepper?.doubleValue = display
        }
    }
    init(_ spec: SettingSpec, value: Int) {
        self.spec = spec; nameLabel = label(spec.label)
        if spec.isBool {
            checkbox = NSButton(checkboxWithTitle: spec.label, target: nil, action: nil); field = nil; stepper = nil
        } else {
            checkbox = nil; field = NSTextField(); stepper = NSStepper()
        }
        super.init(frame: .zero)
        identifier = NSUserInterfaceItemIdentifier(spec.name)
        setAccessibilityLabel(spec.label)
        translatesAutoresizingMaskIntoConstraints = false
        heightAnchor.constraint(equalToConstant: 28).isActive = true
        if let checkbox {
            checkbox.target = self; checkbox.action = #selector(valueChanged)
            fill(checkbox, in: self)
        } else if let field, let stepper {
            let scale = spec.isMultiplier ? 100.0 : 1.0
            let formatter = NumberFormatter(); formatter.locale = Locale(identifier: "en_US_POSIX"); formatter.numberStyle = .decimal
            formatter.usesGroupingSeparator = false
            formatter.minimumFractionDigits = spec.isMultiplier ? 2 : 0; formatter.maximumFractionDigits = spec.isMultiplier ? 2 : 0
            formatter.allowsFloats = spec.isMultiplier
            field.formatter = formatter; field.alignment = .right; field.delegate = self; field.target = self; field.action = #selector(valueChanged)
            field.setAccessibilityLabel(spec.label)
            stepper.minValue = Double(spec.minimum) / scale; stepper.maxValue = Double(spec.maximum) / scale
            stepper.increment = spec.isMultiplier ? 0.25 : 1; stepper.valueWraps = false
            stepper.target = self; stepper.action = #selector(didStep)
            stepper.setAccessibilityLabel(spec.label)
            for view in [nameLabel, field, stepper] { view.translatesAutoresizingMaskIntoConstraints = false; addSubview(view); view.centerYAnchor.constraint(equalTo: centerYAnchor).isActive = true }
            NSLayoutConstraint.activate([nameLabel.leadingAnchor.constraint(equalTo: leadingAnchor), nameLabel.trailingAnchor.constraint(lessThanOrEqualTo: field.leadingAnchor, constant: -8), field.widthAnchor.constraint(equalToConstant: 72), field.trailingAnchor.constraint(equalTo: stepper.leadingAnchor, constant: -4), stepper.trailingAnchor.constraint(equalTo: trailingAnchor)])
        }
        self.value = value
    }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    @objc func didStep() { field?.doubleValue = stepper!.doubleValue; changed?() }
    @objc func valueChanged() { let v = value; value = v; changed?() }
    func controlTextDidChange(_ obj: Notification) { stepper?.doubleValue = field?.doubleValue ?? 0; changed?() }
    func controlTextDidEndEditing(_ obj: Notification) { valueChanged() }
}

final class MainWindow: NSWindowController, NSWindowDelegate {
    let directory: URL
    let permission: AccessibilityPermission
    let diagnostic: Bool
    let text = LiteralTextView(), status = label("Ready"), hotkeyStatus = label("F7 pending (Fn+F7)")
    let progress = NSProgressIndicator()
    private(set) var controls = [String: SettingControl](), orderedControls = [SettingControl]()
    private(set) var sectionLabels = [String]()
    var startButton: NSButton!, stopButton: NSButton!, calibrateButton: NSButton!
    private(set) var cancellation: Cancellation?
    private(set) var calibration: CalibrationWindow?, results: ResultsWindow?
    var quit: (() -> Void)?
    var save: ((Settings) -> Bool)?
    var runtimeFactory: ((Cancellation) -> EngineRuntime)?
    var running = false { didSet { updateControlState() } }
    var calibrationOpen = false { didSet { updateControlState() } }
    private var hotkey: GlobalHotkey?
    private var permissionDialog: PermissionWindow?
    private var lastPermissionState: PermissionState?
    let permissionNotice = label("")
    var permissionButton: NSButton!
    let settingsScroll = NSScrollView()
    let scrollHint = label("Scroll for Difficult keys and Correction settings ↓")
    var permissionPresentation: ((PermissionState) -> Void)?
    private(set) var modalChild: NSWindow?
    init(directory: URL, permission: AccessibilityPermission? = nil, diagnostic: Bool = false) throws {
        self.directory = directory; self.diagnostic = diagnostic
        self.permission = permission ?? (diagnostic ? AccessibilityPermission(requestListing: {}) : AccessibilityPermission())
        let settings = try Settings.load(directory: directory)
        let w = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 1060, height: 740), styleMask: [.titled, .closable, .miniaturizable, .resizable], backing: .buffered, defer: false)
        w.title = "AutoWrite"; w.minSize = NSSize(width: 920, height: 580); w.isReleasedWhenClosed = false
        super.init(window: w); w.delegate = self
        let root = BackgroundView(); w.contentView = root
        let heading = label("AutoWrite", bold: true); heading.font = .boldSystemFont(ofSize: 22)
        let header = stack([heading, NSView(), hotkeyStatus], orientation: .horizontal)
        configurePlainText(text); let editor = textScroll(text)
        let settingsStack = NSStackView(); settingsStack.orientation = .vertical; settingsStack.alignment = .leading; settingsStack.spacing = 4
        let order = ["WordsPerMinute", "ButtonDelaySeconds", "JitterPercent", "KeyPairRhythmPercent", "BurstPercent", "WordPauseMilliseconds", "SentencePauseMilliseconds", "ParagraphPauseMilliseconds", "ThinkingPauseChancePercent", "ThinkingPauseMilliseconds", "KeepTargetWpm", "UppercaseDelayPenaltyPercent", "NumberDelayPenaltyPercent", "PunctuationDelayPenaltyPercent", "ShiftSymbolDelayPenaltyPercent", "NumberTypoMultiplierPercent", "PunctuationTypoMultiplierPercent", "ShiftSymbolTypoMultiplierPercent", "EnableTypos", "TypoChancePercent", "DuplicateChancePercent", "TransposeChancePercent", "CorrectionNoticeMilliseconds", "CorrectionDetectChars", "CorrectionPauseMilliseconds", "CorrectionResumeMilliseconds"]
        let sections = ["WordsPerMinute":"Typing", "JitterPercent":"Rhythm", "KeepTargetWpm":"Difficult keys", "EnableTypos":"Correction"]
        for name in order {
            if let section = sections[name] {
                let title = label(section, bold: true); title.translatesAutoresizingMaskIntoConstraints = false; title.heightAnchor.constraint(equalToConstant: 28).isActive = true
                settingsStack.addArrangedSubview(title); sectionLabels.append(section)
            }
            let control = SettingControl(Settings.spec(name), value: settings[name]); controls[name] = control; orderedControls.append(control)
            control.changed = { [weak self] in self?.updateControlState() }
            settingsStack.addArrangedSubview(control)
            control.widthAnchor.constraint(equalTo: settingsStack.widthAnchor).isActive = true
        }
        settingsScroll.hasVerticalScroller = true; settingsScroll.autohidesScrollers = false; settingsScroll.scrollerStyle = .legacy; settingsScroll.drawsBackground = false
        settingsScroll.documentView = settingsStack; settingsStack.translatesAutoresizingMaskIntoConstraints = false
        NSLayoutConstraint.activate([settingsStack.leadingAnchor.constraint(equalTo: settingsScroll.contentView.leadingAnchor), settingsStack.topAnchor.constraint(equalTo: settingsScroll.contentView.topAnchor), settingsStack.widthAnchor.constraint(equalTo: settingsScroll.contentView.widthAnchor)])
        startButton = button("Type in 3s", target: self, action: #selector(startFromButton)); stopButton = button("Stop", target: self, action: #selector(stop)); calibrateButton = button("Calibrate typing", target: self, action: #selector(openCalibration))
        let buttons = stack([startButton, stopButton, calibrateButton], orientation: .horizontal, spacing: 4)
        scrollHint.font = .systemFont(ofSize: 11); scrollHint.textColor = .secondaryLabelColor
        let side = stack([settingsScroll, scrollHint, buttons]); side.alignment = .width
        let content = stack([editor, side], orientation: .horizontal, spacing: 16)
        content.alignment = .height
        side.widthAnchor.constraint(equalToConstant: 365).isActive = true
        progress.minValue = 0; progress.maxValue = 100; progress.isIndeterminate = false; progress.style = .bar
        progress.widthAnchor.constraint(equalToConstant: 220).isActive = true
        let footer = stack([status, NSView(), progress], orientation: .horizontal)
        permissionButton = button("Allow Accessibility…", target: self, action: #selector(requestPermission))
        let permissionRow = stack([permissionNotice, permissionButton], orientation: .horizontal)
        permissionRow.identifier = NSUserInterfaceItemIdentifier("PermissionNotice")
        let layout = stack([header, permissionRow, content, footer], spacing: 14); layout.alignment = .width
        fill(layout, in: root, inset: 16)
        header.heightAnchor.constraint(equalToConstant: 32).isActive = true; footer.heightAnchor.constraint(equalToConstant: 22).isActive = true
        status.maximumNumberOfLines = 1; status.lineBreakMode = .byTruncatingTail
        updateControlState()
        refreshPermissionStatus()
        w.contentView?.layoutSubtreeIfNeeded()
        if let document = settingsScroll.documentView { document.scroll(NSPoint(x: 0, y: document.isFlipped ? document.bounds.minY : document.bounds.maxY)) }
    }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    func refreshPermissionStatus() {
        let state = permission.state(), missing = state != .allowed
        permissionNotice.stringValue = missing ? "Accessibility permission is required to type into other apps." : ""
        permissionButton.isHidden = !missing
        permissionNotice.superview?.isHidden = !missing
        if missing { setStatus(permission.message(for: state), error: true) }
        else if lastPermissionState != nil && lastPermissionState != .allowed { setStatus(permission.message(for: .allowed)) }
        lastPermissionState = state
    }
    @objc func requestPermission() { explainPermission(permission.state()) }
    private func explainPermission(_ state: PermissionState) {
        refreshPermissionStatus()
        if state == .missing { permission.requestIfMissing() }
        permissionPresentation?(state)
        guard !diagnostic, let window, permissionDialog == nil else { return }
        NSApplication.shared.activate(ignoringOtherApps: true)
        window.makeKeyAndOrderFront(nil)
        let dialog = PermissionWindow(permission: permission, state: state)
        permissionDialog = dialog
        window.beginSheet(dialog.window!) { [weak self] _ in self?.permissionDialog = nil }
    }
    private func presentCalibration(_ child: NSWindow) {
        guard let owner = window else { return }
        modalChild = child
        child.setFrameOrigin(NSPoint(x: owner.frame.midX - child.frame.width / 2, y: owner.frame.midY - child.frame.height / 2))
        if !diagnostic {
            DispatchQueue.main.async { [weak self, weak child] in
                guard let self, let child, self.modalChild === child else { return }
                NSApplication.shared.runModal(for: child)
            }
        }
    }
    private func endCalibrationSheet() {
        if let child = modalChild, NSApplication.shared.modalWindow === child { NSApplication.shared.stopModal(); child.orderOut(nil) }
        modalChild = nil
    }
    func windowShouldClose(_ sender: NSWindow) -> Bool { !calibrationOpen }
    func armHotkey() {
        guard !diagnostic else { return }
        hotkey = GlobalHotkey { [weak self] in self?.requestTyping(buttonDelay: false) }
        let armed = hotkey!.register()
        hotkeyStatus.stringValue = armed ? "F7 armed (Fn+F7)" : "F7 unavailable (Fn+F7)"
        if !armed { setStatus("F7 unavailable: another app may already use this key.", error: true) }
    }
    func currentSettings() -> Settings { var settings = Settings(); for (name, control) in controls { settings[name] = control.value }; return settings }
    func setSettings(_ settings: Settings) { for (name, control) in controls { control.value = settings[name] }; updateControlState() }
    @discardableResult func applyCalibration(_ settings: Settings) -> Bool {
        let previous = currentSettings(); setSettings(settings)
        if save?(currentSettings()) ?? currentSettings().save(directory: directory) { return true }
        setSettings(previous); return false
    }
    func updateControlState() {
        guard startButton != nil else { return }
        let unlocked = !running && !calibrationOpen, typos = controls["EnableTypos"]!.value != 0
        let correction = Set(["TypoChancePercent", "DuplicateChancePercent", "TransposeChancePercent", "CorrectionNoticeMilliseconds", "CorrectionDetectChars", "CorrectionPauseMilliseconds", "CorrectionResumeMilliseconds", "NumberTypoMultiplierPercent", "PunctuationTypoMultiplierPercent", "ShiftSymbolTypoMultiplierPercent"])
        for (name, control) in controls { control.enabled = unlocked && (!correction.contains(name) || typos) }
        text.isEditable = unlocked; text.isSelectable = unlocked
        startButton.isEnabled = unlocked; calibrateButton.isEnabled = unlocked; stopButton.isEnabled = running
        startButton.title = "Type in \(controls["ButtonDelaySeconds"]!.value)s"
    }
    func setStatus(_ value: String, error: Bool = false) { status.stringValue = value; status.textColor = error ? .systemRed : .labelColor }
    @objc func startFromButton() { requestTyping(buttonDelay: true) }
    func requestTyping(buttonDelay: Bool) {
        if calibrationOpen || permissionDialog != nil { return }
        if running { stop(); return }
        let source = units(text.string)
        if source.isEmpty { setStatus("No text", error: true); return }
        let state = permission.state()
        if state != .allowed {
            setStatus(permission.message(for: state), error: true)
            explainPermission(state)
            return
        }
        // Diagnostics can exercise the permission and control path without global input.
        if diagnostic && runtimeFactory == nil { setStatus(permission.message(for: .allowed)); return }
        do {
            var options = TypingOptions(settings: currentSettings())
            options.startDelay = buttonDelay ? currentSettings()["ButtonDelaySeconds"] * 1000 : 0
            options.triggerKey = buttonDelay ? 0 : 98
            options.telemetryPath = try Telemetry.createPath(directory: directory)
            let cancellation = Cancellation(); self.cancellation = cancellation; running = true
            progress.doubleValue = 0; setStatus(options.startDelay > 0 ? "Starting" : "Typing")
            let path = options.telemetryPath
            let factory = runtimeFactory
            DispatchQueue.global(qos: .userInteractive).async { [weak self] in
                let activity = TimingActivity(); defer { activity.end() }
                let runtime = factory?(cancellation) ?? MacRuntime(cancellation: cancellation)
                var failure: Error?
                do { try TypingEngine(runtime).type(source, options: options, progress: { done, total in
                    DispatchQueue.main.async { [weak self] in
                        self?.progress.doubleValue = Double(roundEven(Double(done) * 100 / Double(max(1, total))))
                        self?.setStatus("Typing \(done) / \(total)")
                    }
                }) } catch { failure = error }
                DispatchQueue.main.async {
                    guard let self else { return }; self.cancellation = nil; self.running = false
                    if cancellation.isCancelled { self.setStatus("Stopped") }
                    else if let failure {
                        let cancelled: Bool
                        if case AutoWriteError.cancelled = failure { cancelled = true } else { cancelled = false }
                        self.setStatus(String(describing: failure), error: !cancelled)
                    }
                    else { self.progress.doubleValue = 100; self.setStatus("Done. Log: " + URL(fileURLWithPath: path).lastPathComponent) }
                }
            }
        } catch { setStatus(String(describing: error), error: true) }
    }
    @objc func stop() { cancellation?.cancel(); if running { setStatus("Stopping") } }
    @objc func openCalibration() {
        guard !running && !calibrationOpen else { return }
        calibrationOpen = true
        let current = currentSettings()
        let dialog = CalibrationWindow(current: current, directory: directory)
        calibration = dialog
        dialog.completed = { [weak self] session, warning in
            guard let self else { return }
            self.endCalibrationSheet()
            self.calibration = nil
            guard let session else { self.calibrationOpen = false; return }
            let result = CalibrationAnalysis.analyze(session.keys, seconds: session.seconds, current: current)
            if !warning.isEmpty { result.warning = warning + " " + result.warning }
            let results = ResultsWindow(result: result, apply: { [weak self] in self?.applyCalibration($0) ?? false })
            self.results = results
            results.closed = { [weak self] in self?.endCalibrationSheet(); self?.results = nil; self?.calibrationOpen = false }
            self.presentCalibration(results.window!)
        }
        presentCalibration(dialog.window!)
        dialog.window?.makeFirstResponder(dialog.input)
    }
    func shutdown() { stop(); hotkey?.unregister(); _ = currentSettings().save(directory: directory); calibration?.cancel(); results?.discard() }
    func windowWillClose(_ notification: Notification) { shutdown(); quit?() }
}

final class CaptureAwareMenu: NSMenu {
    var captureInput: () -> CaptureInputView? = { NSApplication.shared.keyWindow?.firstResponder as? CaptureInputView }
    override func performKeyEquivalent(with event: NSEvent) -> Bool {
        if let input = captureInput(), input.consumeKeyEquivalent(event) { return true }
        return super.performKeyEquivalent(with: event)
    }
}
func applicationMenu(target: AnyObject?) -> NSMenu {
    let main = CaptureAwareMenu()
    let app = NSMenuItem(); main.addItem(app); app.submenu = NSMenu(title: "AutoWrite")
    let quit = NSMenuItem(title: "Quit AutoWrite", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q"); app.submenu?.addItem(quit)
    let edit = NSMenuItem(); main.addItem(edit); edit.submenu = NSMenu(title: "Edit")
    let actions: [(String, String, String)] = [("Undo","undo:","z"),("Redo","redo:","Z"),("Cut","cut:","x"),("Copy","copy:","c"),("Paste","paste:","v"),("Select All","selectAll:","a")]
    for (title, selector, key) in actions { edit.submenu?.addItem(NSMenuItem(title: title, action: Selector(selector), keyEquivalent: key)) }
    return main
}

final class PermissionWindow: NSWindowController {
    let message: NSTextField
    var openButton: NSButton!, cancelButton: NSButton!
    init(permission: AccessibilityPermission, state: PermissionState) {
        message = label(permission.message(for: state))
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 540, height: 280), styleMask: [.titled], backing: .buffered, defer: false)
        window.title = state == .restartNeeded ? "Restart AutoWrite" : "Allow AutoWrite to type"
        window.isReleasedWhenClosed = false; window.contentView = BackgroundView()
        super.init(window: window)
        let heading = label(window.title, bold: true); heading.font = .boldSystemFont(ofSize: 16)
        openButton = button("Open Accessibility Settings", target: self, action: #selector(openSettings))
        cancelButton = button("Cancel", target: self, action: #selector(cancel)); cancelButton.keyEquivalent = "\u{1B}"
        let buttons = stack([NSView(), cancelButton, openButton], orientation: .horizontal)
        let root = stack([heading, message, NSView(), buttons], spacing: 14); root.alignment = .width
        fill(root, in: window.contentView!, inset: 24)
        buttons.heightAnchor.constraint(equalToConstant: 32).isActive = true
    }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    @objc func openSettings() { cancel(); AccessibilityPermission.openSettings() }
    @objc func cancel() { if let parent = window?.sheetParent, let window { parent.endSheet(window) }; window?.close() }
}

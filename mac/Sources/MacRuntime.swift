import AppKit
import ApplicationServices
import Carbon
import Darwin

final class Cancellation {
    private let lock = NSRecursiveLock()
    private var stopped = false
    func cancel() { lock.lock(); stopped = true; lock.unlock() }
    var isCancelled: Bool { lock.lock(); defer { lock.unlock() }; return stopped }
    func check() throws { if isCancelled { throw AutoWriteError.cancelled } }
    func postPair(_ body: () -> Void) throws {
        lock.lock(); defer { lock.unlock() }
        if stopped { throw AutoWriteError.cancelled }
        body()
    }
}

// No event is posted by this type: the application and its tests use the same
// encoder, and only MacRuntime's explicitly supplied sink posts to the session.
final class KeyboardEncoder {
    private let source = CGEventSource(stateID: .privateState)
    private var pendingHigh: Unit?
    private var emitted = [Unit]()
    private var deletedLow = false
    func events(for value: Unit) -> [CGEvent] {
        if (0xD800...0xDBFF).contains(value) { pendingHigh = value; return [] }
        if value == 8 {
            if pendingHigh != nil { pendingHigh = nil; return [] }
            if let last = emitted.popLast() {
                if deletedLow && (0xD800...0xDBFF).contains(last) { deletedLow = false; return [] }
                deletedLow = (0xDC00...0xDFFF).contains(last) && emitted.last.map { (0xD800...0xDBFF).contains($0) } == true
            }
            return pair(key: 51, unicode: [])
        }
        deletedLow = false
        var unicode = [value]
        if let high = pendingHigh { unicode = [high, value]; pendingHigh = nil }
        emitted += unicode
        if value == 10 || value == 13 { return pair(key: 36, unicode: []) }
        if value == 9 { return pair(key: 48, unicode: []) }
        return pair(key: 0, unicode: unicode)
    }
    private func pair(key: CGKeyCode, unicode: [Unit]) -> [CGEvent] {
        guard let down = CGEvent(keyboardEventSource: source, virtualKey: key, keyDown: true), let up = CGEvent(keyboardEventSource: source, virtualKey: key, keyDown: false) else { return [] }
        for event in [down, up] {
            event.flags = []
            event.setIntegerValueField(.eventSourceUserData, value: 0x41575445)
            if !unicode.isEmpty { unicode.withUnsafeBufferPointer { event.keyboardSetUnicodeString(stringLength: unicode.count, unicodeString: $0.baseAddress!) } }
        }
        return [down, up]
    }
}

final class PrecisePauses {
    private var deadline = mach_absolute_time()
    private var info = mach_timebase_info_data_t()
    let cancellation: Cancellation
    init(_ cancellation: Cancellation) { self.cancellation = cancellation; mach_timebase_info(&info) }
    func reset() { deadline = mach_absolute_time() }
    func sleep(_ ms: Int) throws {
        try cancellation.check()
        if ms <= 0 { return }
        deadline += UInt64(ms) * 1_000_000 * UInt64(info.denom) / UInt64(info.numer)
        let slice = UInt64(5_000_000) * UInt64(info.denom) / UInt64(info.numer)
        while mach_absolute_time() < deadline {
            try cancellation.check()
            mach_wait_until(min(deadline, mach_absolute_time() + slice))
        }
        try cancellation.check()
    }
    // Time spent waiting for a human/focus is not engine motor work. Resetting
    // prevents catching up with a burst of keys after the target returns.
    func suspend(_ ms: Int) throws { reset(); try sleep(ms); reset() }
}

// CGWindowListCreate is a public C API in CGWindow.h, unavailable to Swift's
// importer because this CFArray contains raw window IDs rather than CF objects.
// Read those IDs with CFArrayGetValueAtIndex, never through an NSArray bridge.
private typealias WindowListCreate = @convention(c) (UInt32, UInt32) -> Unmanaged<CFArray>?
private let windowListCreate: WindowListCreate? = {
    guard let symbol = dlsym(UnsafeMutableRawPointer(bitPattern: -2), "CGWindowListCreate") else { return nil }
    return unsafeBitCast(symbol, to: WindowListCreate.self)
}()
private func orderedWindowIDs() -> [UInt32]? {
    guard let create = windowListCreate, let list = create(CGWindowListOption([.optionOnScreenOnly, .excludeDesktopElements]).rawValue, kCGNullWindowID)?.takeRetainedValue() else { return nil }
    return (0..<CFArrayGetCount(list)).map { UInt32(truncatingIfNeeded: UInt(bitPattern: CFArrayGetValueAtIndex(list, $0))) }
}
private final class ForegroundIdentityCache {
    let lock = NSLock()
    var pid: pid_t = 0, identity: UInt64 = 0, order = [UInt32]()
    func current() -> UInt64 {
        lock.lock(); defer { lock.unlock() }
        guard let front = NSWorkspace.shared.frontmostApplication?.processIdentifier else { return 0 }
        let ids = orderedWindowIDs()
        // Only reuse metadata if BOTH the front application and the complete
        // front-to-back window order are unchanged. Closed/new/reordered windows
        // and application switches all force a fresh resolution.
        if let ids, front == pid && ids == order && identity != 0 { return identity }
        var result = UInt64(UInt32(bitPattern: front))
        if let windows = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as? [[String: Any]],
           let window = windows.first(where: { ($0[kCGWindowOwnerPID as String] as? Int) == Int(front) && ($0[kCGWindowLayer as String] as? Int) == 0 }),
           let number = window[kCGWindowNumber as String] as? UInt32 { result = UInt64(number) << 32 | UInt64(UInt32(bitPattern: front)) }
        pid = front; order = ids ?? []; identity = result
        return result
    }
}
private let foregroundIdentityCache = ForegroundIdentityCache()
func foregroundWindowIdentity() -> UInt64 { foregroundIdentityCache.current() }

final class MacRuntime: EngineRuntime {
    let cancellation: Cancellation, pauses: PrecisePauses
    let encoder = KeyboardEncoder()
    let seed: Int
    let start = ProcessInfo.processInfo.systemUptime
    var expectedWindow: UInt64 = 0
    var guardBeforePost: (() throws -> Void)?
    var physicalKeyState: (CGKeyCode) -> Bool = { CGEventSource.keyState(.hidSystemState, key: $0) }
    var modifierDown: () -> Bool = { false }
    var windowIdentity: () -> UInt64 = foregroundWindowIdentity
    var preparationCheckpoint: (() -> Void)?
    private var hasPosted = false
    private var waitingForWindow = false
    private var checkedWindow: (identity: UInt64, at: Double)?
    var sink: (CGEvent) -> Void
    static let modifierKeys = [54, 55, 56, 60, 58, 61, 59, 62, 63]
    init(cancellation: Cancellation, seed: Int = Int.random(in: 0...Int(Int32.max)), sink: @escaping (CGEvent) -> Void = { $0.post(tap: .cghidEventTap) }) {
        self.cancellation = cancellation; self.pauses = PrecisePauses(cancellation); self.seed = seed; self.sink = sink
        modifierDown = { [unowned self] in Self.modifierKeys.contains { self.physicalKeyState(CGKeyCode($0)) } }
    }
    func createRandom() -> DotRandom { DotRandom(seed) }
    func checkCancellation() throws { preparationCheckpoint?(); try cancellation.check() }
    func foregroundWindow() throws -> UInt64 {
        try cancellation.check(); try guardBeforePost?()
        let window = windowIdentity()
        if expectedWindow == 0 { expectedWindow = window }
        waitingForWindow = expectedWindow != 0 && window != expectedWindow
        checkedWindow = (window, ProcessInfo.processInfo.systemUptime)
        return window
    }
    func isKeyDown(_ key: Int) throws -> Bool { try cancellation.check(); return physicalKeyState(CGKeyCode(key)) }
    func releaseModifiers() throws {
        // Wait for physical release, including Fn from Fn+F7. Posting key-up
        // events cannot safely release a key that the human is still holding.
        while modifierDown() { try pauses.suspend(5); try guardBeforePost?() }
    }
    func sendCharacter(_ value: Unit) throws {
        let events = encoder.events(for: value)
        guard !events.isEmpty else { try cancellation.check(); return }
        while true {
            try cancellation.check(); try guardBeforePost?()
            let window: UInt64
            if let checked = checkedWindow, ProcessInfo.processInfo.systemUptime - checked.at < 0.005 { window = checked.identity }
            else { window = windowIdentity() }
            checkedWindow = nil
            if expectedWindow != 0 && window != expectedWindow { try pauses.suspend(5); continue }
            // This is the final state check, after every window wait and directly
            // before the indivisible down/up pair. Never throw between its events.
            if modifierDown() { try pauses.suspend(5); continue }
            break
        }
        if !hasPosted { pauses.reset(); hasPosted = true }
        try cancellation.postPair { for event in events { sink(event) } }
    }
    func sleep(_ milliseconds: Int) throws {
        checkedWindow = nil
        try guardBeforePost?()
        if waitingForWindow { try pauses.suspend(milliseconds) } else { try pauses.sleep(milliseconds) }
        try guardBeforePost?()
    }
    var milliseconds: Double { (ProcessInfo.processInfo.systemUptime - start) * 1000 }
}

final class TimingActivity {
    private var activity: NSObjectProtocol?
    init() { activity = ProcessInfo.processInfo.beginActivity(options: [.userInitiatedAllowingIdleSystemSleep, .latencyCritical], reason: "Maintain AutoWrite typing pauses while in the background") }
    func end() { if let activity { ProcessInfo.processInfo.endActivity(activity); self.activity = nil } }
    deinit { end() }
}

enum PermissionState { case allowed, missing, restartNeeded }
final class AccessibilityPermission {
    var trusted: () -> Bool, canPost: () -> Bool
    let requestListing: () -> Void
    init(trusted: @escaping () -> Bool = { AXIsProcessTrusted() }, canPost: @escaping () -> Bool = { CGPreflightPostEventAccess() }, requestListing: @escaping () -> Void = {
        let options = [kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true] as CFDictionary
        _ = AXIsProcessTrustedWithOptions(options)
    }) { self.trusted = trusted; self.canPost = canPost; self.requestListing = requestListing }
    func state() -> PermissionState {
        if !trusted() { return .missing }
        return canPost() ? .allowed : .restartNeeded
    }
    func requestIfMissing() { if state() == .missing { requestListing() } }
    func message(for value: PermissionState) -> String {
        switch value {
        case .allowed: return "Accessibility permission granted. Ready."
        case .missing: return "AutoWrite needs Accessibility permission to type into other apps. Open System Settings > Privacy & Security > Accessibility and allow AutoWrite. After rebuilding, remove the old AutoWrite entry and allow this build again. If permission is enabled but typing is still blocked, quit and reopen AutoWrite."
        case .restartNeeded: return "Accessibility permission is enabled, but macOS still blocks this build from typing. Quit and reopen AutoWrite. If it is still blocked, remove the old AutoWrite entry in Privacy & Security > Accessibility, then allow this build again."
        }
    }
    static func openSettings() { if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility") { NSWorkspace.shared.open(url) } }
}

final class GlobalHotkey {
    private var ref: EventHotKeyRef?, handler: EventHandlerRef?
    private var held = false
    let action: () -> Void
    init(action: @escaping () -> Void) { self.action = action }
    func register() -> Bool {
        var events = [EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyPressed)), EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyReleased))]
        let context = Unmanaged.passUnretained(self).toOpaque()
        let status = InstallEventHandler(GetApplicationEventTarget(), { _, event, userData -> OSStatus in
            guard let event, let userData else { return OSStatus(eventNotHandledErr) }
            let owner = Unmanaged<GlobalHotkey>.fromOpaque(userData).takeUnretainedValue()
            var id = EventHotKeyID()
            guard GetEventParameter(event, EventParamName(kEventParamDirectObject), EventParamType(typeEventHotKeyID), nil, MemoryLayout<EventHotKeyID>.size, nil, &id) == noErr, id.signature == 0x41575445 else { return OSStatus(eventNotHandledErr) }
            owner.handle(pressed: GetEventKind(event) == UInt32(kEventHotKeyPressed))
            return noErr
        }, events.count, &events, context, &handler)
        guard status == noErr else { return false }
        let id = EventHotKeyID(signature: 0x41575445, id: 7)
        if RegisterEventHotKey(UInt32(kVK_F7), 0, id, GetApplicationEventTarget(), 0, &ref) != noErr { unregister(); return false }
        return true
    }
    // Function keys carry the function flag even when Fn is not physically held.
    // The proof posts this pair only after its own window has keyboard focus.
    static func triggerEvents() -> [CGEvent] {
        let source = CGEventSource(stateID: .privateState)
        return [true, false].compactMap { down in
            guard let event = CGEvent(keyboardEventSource: source, virtualKey: CGKeyCode(kVK_F7), keyDown: down) else { return nil }
            event.flags = .maskSecondaryFn
            event.setIntegerValueField(.keyboardEventAutorepeat, value: 0)
            return event
        }
    }
    func handle(pressed: Bool) {
        if !pressed { held = false }
        else if !held { held = true; action() }
    }
    func unregister() { if let ref { UnregisterEventHotKey(ref); self.ref = nil }; if let handler { RemoveEventHandler(handler); self.handler = nil } }
    deinit { unregister() }
}

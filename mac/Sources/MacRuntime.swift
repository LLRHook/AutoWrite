import AppKit
import ApplicationServices
import Carbon
import Darwin

final class Cancellation {
    private let lock = NSLock()
    private var stopped = false
    func cancel() { lock.lock(); stopped = true; lock.unlock() }
    var isCancelled: Bool { lock.lock(); defer { lock.unlock() }; return stopped }
    func check() throws { if isCancelled { throw AutoWriteError.cancelled } }
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

func foregroundWindowIdentity() -> UInt64 {
    guard let pid = NSWorkspace.shared.frontmostApplication?.processIdentifier else { return 0 }
    if let windows = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as? [[String: Any]],
       let window = windows.first(where: { ($0[kCGWindowOwnerPID as String] as? Int) == Int(pid) && ($0[kCGWindowLayer as String] as? Int) == 0 }),
       let number = window[kCGWindowNumber as String] as? UInt32 { return UInt64(number) << 32 | UInt64(UInt32(bitPattern: pid)) }
    return UInt64(UInt32(bitPattern: pid))
}

final class MacRuntime: EngineRuntime {
    let cancellation: Cancellation, pauses: PrecisePauses
    let encoder = KeyboardEncoder()
    let seed: Int
    let start = ProcessInfo.processInfo.systemUptime
    var expectedWindow: UInt64 = 0
    var guardBeforePost: (() throws -> Void)?
    var modifierDown: () -> Bool = { modifierKeys.contains { CGEventSource.keyState(.hidSystemState, key: CGKeyCode($0)) } }
    var windowIdentity: () -> UInt64 = foregroundWindowIdentity
    var sink: (CGEvent) -> Void
    static let modifierKeys = [54, 55, 56, 60, 58, 61, 59, 62, 63]
    init(cancellation: Cancellation, seed: Int = Int.random(in: 0...Int(Int32.max)), sink: @escaping (CGEvent) -> Void = { $0.post(tap: .cghidEventTap) }) {
        self.cancellation = cancellation; self.pauses = PrecisePauses(cancellation); self.seed = seed; self.sink = sink
    }
    func createRandom() -> DotRandom { DotRandom(seed) }
    func foregroundWindow() throws -> UInt64 {
        try cancellation.check(); try guardBeforePost?()
        let window = windowIdentity()
        if expectedWindow == 0 { expectedWindow = window }
        return window
    }
    func isKeyDown(_ key: Int) throws -> Bool { try cancellation.check(); return CGEventSource.keyState(.hidSystemState, key: CGKeyCode(key)) }
    func releaseModifiers() throws {
        // Wait for physical release, including Fn from Fn+F7. Posting key-up
        // events cannot safely release a key that the human is still holding.
        while modifierDown() { try pauses.suspend(5); try guardBeforePost?() }
    }
    func sendCharacter(_ value: Unit) throws {
        try cancellation.check(); try releaseModifiers()
        if expectedWindow != 0 {
            while windowIdentity() != expectedWindow { try guardBeforePost?(); try pauses.suspend(50) }
        }
        try guardBeforePost?()
        for event in encoder.events(for: value) { try cancellation.check(); try guardBeforePost?(); sink(event) }
    }
    func sleep(_ milliseconds: Int) throws { try guardBeforePost?(); try pauses.sleep(milliseconds); try guardBeforePost?() }
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
    var state: () -> PermissionState
    init(state: @escaping () -> PermissionState = {
        if !AXIsProcessTrusted() { return .missing }
        return CGPreflightPostEventAccess() ? .allowed : .restartNeeded
    }) { self.state = state }
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
            if GetEventKind(event) == UInt32(kEventHotKeyReleased) { owner.held = false }
            else if !owner.held { owner.held = true; owner.action() }
            return noErr
        }, events.count, &events, context, &handler)
        guard status == noErr else { return false }
        let id = EventHotKeyID(signature: 0x41575445, id: 7)
        if RegisterEventHotKey(UInt32(kVK_F7), 0, id, GetApplicationEventTarget(), 0, &ref) != noErr { unregister(); return false }
        return true
    }
    func unregister() { if let ref { UnregisterEventHotKey(ref); self.ref = nil }; if let handler { RemoveEventHandler(handler); self.handler = nil } }
    deinit { unregister() }
}

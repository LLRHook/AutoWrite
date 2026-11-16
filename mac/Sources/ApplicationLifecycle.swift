import AppKit

struct RunningCopy {
    let pid: pid_t, bundleIdentifier: String?
}
enum AutoWriteInstances {
    static let bundleIdentifier = "org.autowrite.AutoWrite"
    static func running() -> [RunningCopy] {
        NSWorkspace.shared.runningApplications.map { RunningCopy(pid: $0.processIdentifier, bundleIdentifier: $0.bundleIdentifier) }
    }
    static func other(_ copies: [RunningCopy], ownPID: pid_t = ProcessInfo.processInfo.processIdentifier) -> pid_t? {
        copies.first { $0.pid != ownPID && $0.bundleIdentifier == bundleIdentifier }?.pid
    }
    static func activate(_ pid: pid_t) { NSRunningApplication(processIdentifier: pid)?.activate(options: [.activateAllWindows]) }
}

final class ApplicationDelegate: NSObject, NSApplicationDelegate {
    var main: MainWindow?
    var runningCopies: () -> [RunningCopy] = AutoWriteInstances.running
    var activateCopy: (pid_t) -> Void = AutoWriteInstances.activate
    var terminate: () -> Void = { NSApplication.shared.terminate(nil) }
    func applicationDidFinishLaunching(_ notification: Notification) {
        if let first = AutoWriteInstances.other(runningCopies()) {
            activateCopy(first); terminate(); return
        }
        do {
            main = try MainWindow(directory: dataDirectory())
            main?.quit = { NSApplication.shared.terminate(nil) }
            NSApplication.shared.mainMenu = applicationMenu(target: self)
            main?.window?.center(); main?.showWindow(nil); main?.armHotkey()
        } catch {
            let alert = NSAlert(); alert.messageText = "AutoWrite could not start"; alert.informativeText = String(describing: error); alert.runModal(); NSApplication.shared.terminate(nil)
        }
    }
    func applicationDidBecomeActive(_ notification: Notification) { main?.refreshPermissionStatus() }
    func applicationWillTerminate(_ notification: Notification) { main?.shutdown() }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }
}

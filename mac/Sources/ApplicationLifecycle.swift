import AppKit

final class ApplicationDelegate: NSObject, NSApplicationDelegate {
    var main: MainWindow?
    func applicationDidFinishLaunching(_ notification: Notification) {
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

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
    func applicationWillTerminate(_ notification: Notification) { main?.shutdown() }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }
}

if CommandLine.arguments.count > 1 {
    exit(LiveTypingProbe.run(arguments: Array(CommandLine.arguments.dropFirst())))
}
let app = NSApplication.shared
let delegate = ApplicationDelegate()
app.delegate = delegate
app.setActivationPolicy(.regular)
app.run()

import AppKit

// This mode exists only in the test executable. It runs the real AppKit loop,
// sheet, button action, terminate and production termination delegate in a child.
private final class RestartQuitDelegate: NSObject, NSApplicationDelegate {
    let production = ApplicationDelegate()
    // The test builds only its invisible main window. Do not invoke normal
    // startup when NSApplication.run emits its launch notification.
    func applicationWillTerminate(_ notification: Notification) { production.applicationWillTerminate(notification) }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { production.applicationShouldTerminateAfterLastWindowClosed(sender) }
}

enum RestartQuitChild {
    static func run(_ directory: URL) -> Never {
        guard ProcessInfo.processInfo.environment["AUTOWRITE_DATA_DIR"] == directory.path else { exit(91) }
        let app = NSApplication.shared
        app.setActivationPolicy(.prohibited)
        app.finishLaunching()
        let delegate = RestartQuitDelegate()
        do {
            let permission = AccessibilityPermission(trusted: { true }, canPost: { false }, requestListing: { exit(92) })
            let main = try MainWindow(directory: directory, permission: permission, diagnostic: true, offscreenPresentation: true)
            delegate.production.main = main; app.delegate = delegate
            main.controls["WordsPerMinute"]!.value = 137
            main.window!.orderBack(nil)
            DispatchQueue.main.async {
                main.requestTyping(buttonDelay: false)
                guard let dialog = main.permissionDialog, main.window!.attachedSheet === dialog.window,
                      dialog.window!.sheetParent === main.window, dialog.window!.alphaValue == 0,
                      dialog.window!.ignoresMouseEvents, main.window!.alphaValue == 0,
                      NSWorkspace.shared.frontmostApplication?.processIdentifier != ProcessInfo.processInfo.processIdentifier else { exit(93) }
                do { try "restart sheet attached; invisible; activation prohibited".write(to: directory.appendingPathComponent("sheet-attached"), atomically: true, encoding: .utf8) } catch { exit(94) }
                // No replacement quit callback: this is the actual button action.
                dialog.quitButton.performClick(nil)
            }
            withExtendedLifetime(delegate) { app.run() }
            exit(95) // terminate must exit, rather than merely stop the run loop.
        } catch { exit(96) }
    }
}

extension TestRunner {
    func restartQuit(_ directory: URL) throws {
        test("restart permission sheet Quit terminates a real child and saves settings") {
            let data = directory.appendingPathComponent("restart-quit-child")
            try FileManager.default.createDirectory(at: data, withIntermediateDirectories: true)
            let child = Process(), output = Pipe()
            child.executableURL = URL(fileURLWithPath: CommandLine.arguments[0])
            child.arguments = ["--restart-quit-child", data.path]
            var environment = ProcessInfo.processInfo.environment; environment["AUTOWRITE_DATA_DIR"] = data.path
            child.environment = environment; child.standardOutput = output; child.standardError = output
            let ended = DispatchSemaphore(value: 0)
            child.terminationHandler = { _ in ended.signal() }
            try child.run()
            let completed = ended.wait(timeout: .now() + 4) == .success
            if !completed {
                child.terminate()
                if ended.wait(timeout: .now() + 1) != .success { kill(child.processIdentifier, SIGKILL) }
            }
            child.waitUntilExit()
            let log = String(decoding: output.fileHandleForReading.readDataToEndOfFile(), as: UTF8.self)
            try require(FileManager.default.fileExists(atPath: data.appendingPathComponent("sheet-attached").path), "child never presented the real restart sheet: " + log)
            try require(completed, "Quit left the child alive after four seconds: " + log)
            try equal(child.terminationStatus, 0, "real terminate failed: " + log)
            try require(FileManager.default.fileExists(atPath: data.appendingPathComponent("settings.ini").path), "Quit did not save a settings file")
            try equal(try Settings.load(directory: data)["WordsPerMinute"], 137, "Quit did not save the edited setting")
        }
    }
}

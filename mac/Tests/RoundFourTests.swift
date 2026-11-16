import AppKit

extension TestRunner {
    func roundFour(_ directory: URL) throws {
        test("calibration timer continues in the event-tracking run-loop mode") {
            let capture = CalibrationWindow(current: Settings(), directory: directory.appendingPathComponent("tracking-timer"))
            defer { capture.cancel() }
            var completed = false
            capture.completed = { session, _ in completed = session?.finished == true }
            capture.length.selectItem(at: 0)
            capture.input.keyDown(with: key(83, timestamp: ProcessInfo.processInfo.systemUptime - 29.9))
            let deadline = Date(timeIntervalSinceNow: 0.5)
            // Run only the mode used while dragging/tracking. No direct tick or
            // default-mode turn can end the test on the timer's behalf.
            while !completed && Date() < deadline {
                _ = RunLoop.main.run(mode: .eventTracking, before: Date(timeIntervalSinceNow: 0.01))
            }
            try require(completed, "clock froze while the run loop was tracking")
        }
        test("400-digit positive and negative edits clamp every numeric field without crashing") {
            let main = try MainWindow(directory: directory.appendingPathComponent("large-numbers"), diagnostic: true)
            defer { main.shutdown(); main.window?.close() }
            let digits = String(repeating: "9", count: 400)
            for control in main.orderedControls where !control.spec.isBool {
                for (text, expected) in [(digits, control.spec.maximum), ("-" + digits, control.spec.minimum)] {
                    control.field!.stringValue = text
                    control.controlTextDidEndEditing(Notification(name: NSControl.textDidEndEditingNotification, object: control.field!))
                    try equal(control.value, expected, "huge edit \(control.spec.name)")
                    try equal(control.field!.doubleValue, Double(expected) / (control.spec.isMultiplier ? 100 : 1), "clamped display")
                }
            }
        }
        test("whitespace around valid numeric edits is trimmed in every numeric field") {
            let main = try MainWindow(directory: directory.appendingPathComponent("number-whitespace"), diagnostic: true)
            defer { main.shutdown(); main.window?.close() }
            for control in main.orderedControls where !control.spec.isBool {
                let raw = control.spec.isMultiplier ? "1.5" : "7", expected = control.spec.clamp(control.spec.isMultiplier ? 150 : 7)
                control.field!.stringValue = " \t" + raw + "\r\n "
                control.controlTextDidEndEditing(Notification(name: NSControl.textDidEndEditingNotification, object: control.field!))
                try equal(control.value, expected, "trim \(control.spec.name)")
            }
        }
    }
}

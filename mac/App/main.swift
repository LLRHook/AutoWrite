import AppKit

if CommandLine.arguments.count > 1 {
    exit(LiveTypingProbe.run(arguments: Array(CommandLine.arguments.dropFirst())))
}
let app = NSApplication.shared
let delegate = ApplicationDelegate()
app.delegate = delegate
app.setActivationPolicy(.regular)
app.run()

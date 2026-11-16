import Foundation

extension TestRunner {
    func probeScript(_ directory: URL) throws {
        test("owner script allows launch delay plus app lifetime and cleans its child and work directory") {
            let root = directory.appendingPathComponent("owner-script-limits"), mac = root.appendingPathComponent("mac"), temporary = root.appendingPathComponent("temporary")
            let app = root.appendingPathComponent("dist/AutoWrite.app/Contents/MacOS")
            for folder in [mac,temporary,app] { try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true) }
            let workRecord = root.appendingPathComponent("work"), allowanceRecord = root.appendingPathComponent("allowance"), timerRecord = root.appendingPathComponent("timer")
            let stub = root.appendingPathComponent("launch-stub"), timer = root.appendingPathComponent("timer-stub")
            // This harmless substitute is used only in a copied script. Never
            // invoke Launch Services, a real app, permission APIs or input here.
            let launch = """
            #!/bin/bash
            set -eu
            while [[ "$1" != --owner-proof-directory ]]; do shift; done
            work="$2"
            printf '%s' "$work" > '\(workRecord.path)'
            sleep 0.15
            deadline="$(cat "$work/deadline")"
            printf '%s' "$((deadline - $(date +%s)))" > '\(allowanceRecord.path)'
            printf '%s' "$$" > "$work/proof.pid"
            sleep 0.15
            printf '%s' 'exit=0; stub completed; expected_length=0; received_length=0; first_difference=-1' > "$work/data/live-probe.txt"
            """
            let timerText = """
            #!/bin/bash
            printf '%s' "$1" > '\(timerRecord.path)'
            exec /bin/sleep "$1"
            """
            for (url, text) in [(stub,launch),(timer,timerText),(app.appendingPathComponent("AutoWrite"),"#!/bin/bash\nexit 99\n")] {
                try text.write(to: url, atomically: true, encoding: .utf8)
                try FileManager.default.setAttributes([.posixPermissions:0o755], ofItemAtPath: url.path)
            }
            let source = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().appendingPathComponent("smoke.sh")
            var script = try String(contentsOf: source, encoding: .utf8)
            try require(script.contains("/usr/bin/open -W -n") && script.contains("( sleep \"$watchdog_seconds\""), "script fixture cannot replace launch boundaries")
            script = script.replacingOccurrences(of: "/usr/bin/open -W -n", with: "'" + stub.path + "' -W -n")
                .replacingOccurrences(of: "( sleep \"$watchdog_seconds\"", with: "( '" + timer.path + "' \"$watchdog_seconds\"")
            let copied = mac.appendingPathComponent("smoke.sh"); try script.write(to: copied, atomically: true, encoding: .utf8)
            let process = Process(), output = Pipe()
            process.executableURL = URL(fileURLWithPath: "/bin/bash"); process.arguments = [copied.path,"--live-typing"]
            var environment = ProcessInfo.processInfo.environment; environment["TMPDIR"] = temporary.path
            process.environment = environment; process.standardOutput = output; process.standardError = output
            try process.run(); let printed = String(decoding: output.fileHandleForReading.readDataToEndOfFile(), as: UTF8.self); process.waitUntilExit()
            try equal(process.terminationStatus, 0, "delayed owner stub: " + printed)
            let allowance = Int(try String(contentsOf: allowanceRecord, encoding: .utf8))!, watchdog = Int(try String(contentsOf: timerRecord, encoding: .utf8))!
            try require(allowance >= 29 && allowance <= 30, "launch allowance is not 30 seconds")
            try require(watchdog >= 30 + 15 + 5, "script watchdog can interrupt an allowed launch and full app run")
            let work = try String(contentsOf: workRecord, encoding: .utf8)
            try require(!FileManager.default.fileExists(atPath: work), "owner work directory left behind")
            try require(printed.contains("exit=0; stub completed"), "owner result not read before cleanup")
        }
    }
}

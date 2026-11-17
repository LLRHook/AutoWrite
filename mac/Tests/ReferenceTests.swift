import Foundation
import CryptoKit

struct TestFailure: Error, CustomStringConvertible { let description: String }
func require(_ condition: @autoclosure () -> Bool, _ message: String) throws { if !condition() { throw TestFailure(description: message) } }
func equal<T: Equatable>(_ actual: T, _ expected: T, _ context: String) throws {
    if actual != expected { throw TestFailure(description: "\(context): expected \(String(describing: expected).prefix(250)); got \(String(describing: actual).prefix(250))") }
}
func unescape(_ text: String) -> [Unit] {
    let input = units(text); var output = [Unit](), i = 0
    while i < input.count {
        if input[i] != 92 { output.append(input[i]); i += 1; continue }
        i += 1
        switch input[i] {
        case 92: output.append(92)
        case 116: output.append(9)
        case 110: output.append(10)
        case 114: output.append(13)
        case 117: output.append(Unit(string(Array(input[(i + 1)...(i + 4)])), radix: 16)!); i += 4
        default: fatalError("Invalid fixture escape")
        }
        i += 1
    }
    return output
}
func escape(_ text: [Unit]) -> String {
    text.map { c in
        switch c { case 92: return "\\\\"; case 9: return "\\t"; case 10: return "\\n"; case 13: return "\\r"; case 0..<32, 127...UInt16.max: return String(format: "\\u%04X", Int(c)); default: return string([c]) }
    }.joined()
}
final class Fixtures {
    let root: URL
    var counts = [String: Int]()
    init(root: URL) { self.root = root }
    var countSummary: String { counts.keys.sorted().map { "\($0)=\(counts[$0]!)" }.joined(separator: ", ") }
    func files(_ folder: String) throws -> [String] {
        try FileManager.default.contentsOfDirectory(atPath: root.appendingPathComponent(folder).path).filter { $0.hasSuffix(".tsv") }.sorted().map { folder + "/" + $0 }
    }
    func verifyTrackedFiles() throws {
        let command = Process(), output = Pipe()
        command.executableURL = URL(fileURLWithPath: "/usr/bin/git")
        command.arguments = ["-C", root.deletingLastPathComponent().deletingLastPathComponent().path, "ls-files", "reference/data"]
        command.standardOutput = output
        try command.run(); let data = output.fileHandleForReading.readDataToEndOfFile(); command.waitUntilExit()
        let paths = String(decoding: data, as: UTF8.self).split(separator: "\n").map(String.init)
        try require(command.terminationStatus == 0 && !paths.isEmpty, "cannot read the tracked reference manifest")
        for path in paths { _ = try Data(contentsOf: root.deletingLastPathComponent().deletingLastPathComponent().appendingPathComponent(path)) }
    }
    func read(_ file: String) throws -> String { try String(contentsOf: root.appendingPathComponent(file), encoding: .utf8) }
    func rows(_ file: String) throws -> [[String]] {
        let result = try read(file).components(separatedBy: "\n").filter { !$0.isEmpty && !$0.hasPrefix("#") }.map { $0.components(separatedBy: "\t") }
        try require(!result.isEmpty, "empty reference file: \(file)")
        if !file.contains("/") { counts[String(file.dropLast(4))] = result.count }
        if file == "analysis/index.tsv" { counts["analysis"] = result.count }
        return result
    }
}
final class TestRunner {
    var passed = 0, failed = 0, referenceCases = 0, skipped = 0
    let filter = ProcessInfo.processInfo.environment["AUTOWRITE_TEST_FILTER"]
    func test(_ name: String, _ body: () throws -> Void) {
        if let filter, !name.localizedCaseInsensitiveContains(filter), name != "test setup" { skipped += 1; return }
        do { try body(); passed += 1; print("PASS \(name)") }
        catch { failed += 1; print("FAIL \(name): \(error)") }
    }
    func core(_ f: Fixtures, _ directory: URL) throws {
        for row in try f.rows("random.tsv") {
            test("reference random \(row[0]) \(row[1])") {
                let r = DotRandom(Int(row[0])!), call = row[1]
                let expected = row[2].split(separator: " ").map(String.init)
                var actual = [String]()
                if call == "Mixed" {
                    for _ in 0..<10 { actual += [String(format: "%016llX", r.double().bitPattern), String(r.next(8, 24)), String(r.next(100)), String(r.next())] }
                } else {
                    for _ in expected.indices {
                        if call == "Next()" { actual.append(String(r.next())) }
                        else if call == "NextDouble()" { actual.append(String(format: "%016llX", r.double().bitPattern)) }
                        else {
                            let arguments = call.dropFirst(5).dropLast().split(separator: ",").map { Int(trim(String($0)))! }
                            actual.append(String(arguments.count == 1 ? r.next(arguments[0]) : r.next(arguments[0], arguments[1])))
                        }
                    }
                }
                try equal(actual, expected, "sequence"); referenceCases += 1
            }
        }
        for row in try f.rows("format.tsv") {
            test("reference format \(row[0])") {
                let value = Double(bitPattern: UInt64(row[0], radix: 16)!)
                for places in 1...3 { try equal(fixed(value, places), row[places + 1], "F\(places)") }
                try equal(Double(roundEven(value)), Double(row[5])!, "round even")
                try equal(Int(value), Int(row[6])!, "integer cast"); referenceCases += 1
            }
        }
        test("reference passage") { let text = try f.read("passage.txt"); f.counts["passage"] = text.split(separator: "\n").count; try equal(calibrationPassage, unescape(trim(text)), "passage"); referenceCases += 1 }
        let expectedLogs = Dictionary(uniqueKeysWithValues: try f.rows("engine-logs.tsv").map { ($0[0], $0[1]) })
        let engines = try f.rows("engine.tsv")
        for row in engines {
            test("reference engine \(row[0])") {
                let runtime = VirtualRuntime(seed: Int(row[1])!)
                let options = TypingOptions(settings: Settings(line: row[6]))
                try TypingEngine(runtime).type(unescape(row[7]), options: options)
                let expected = expectedLogs[row[0]]!
                if runtime.log != expected {
                    let a = Array(runtime.log.utf8), b = Array(expected.utf8)
                    let first = Array(zip(a, b)).firstIndex { $0 != $1 } ?? min(a.count, b.count)
                    throw TestFailure(description: "log differs at byte \(first); expected \(String(expected.dropFirst(max(0, first - 40)).prefix(120))); got \(String(runtime.log.dropFirst(max(0, first - 40)).prefix(120)))")
                }
                try equal(runtime.time, Int(row[2])!, "final milliseconds")
                try equal(runtime.keys.count, Int(row[3])!, "key count")
                let hash = SHA256.hash(data: Data(runtime.log.utf8)).map { String(format: "%02X", $0) }.joined()
                try equal(hash, row[4], "SHA256"); referenceCases += 1
            }
        }
        print("REFERENCE engine data: \(engines.count) runs, including \(engines.filter { $0[0].hasPrefix("g") }.count) golden hashes")
        for row in try f.rows("settings-load.tsv") {
            test("reference settings load \(row[0])") {
                let path = directory.appendingPathComponent("settings.ini")
                if row[1] == "<missing>" { try? FileManager.default.removeItem(at: path) }
                else { try string(unescape(row[1])).write(to: path, atomically: true, encoding: .utf8) }
                try equal(try Settings.load(directory: directory).line, row[2], "settings"); referenceCases += 1
            }
        }
        for row in try f.rows("settings-load-bytes.tsv") {
            test("reference settings bytes \(row[0])") {
                let hex = Array(row[1]), bytes = stride(from: 0, to: hex.count, by: 2).map { UInt8(String(hex[$0...($0 + 1)]), radix: 16)! }
                try Data(bytes).write(to: directory.appendingPathComponent("settings.ini"))
                try equal(try Settings.load(directory: directory).line, row[2], "byte-decoded settings"); referenceCases += 1
            }
        }
        for row in try f.rows("settings-save.tsv") {
            test("reference settings save \(row[0])") {
                try require(Settings(line: row[1]).save(directory: directory), "save failed")
                let text = try String(contentsOf: directory.appendingPathComponent("settings.ini"), encoding: .utf8)
                try equal(escape(units(text)), row[2], "saved file"); referenceCases += 1
            }
        }
        let ranges = try f.rows("settings-ranges.tsv")
        test("reference 26 settings ranges, labels, defaults and stored order") {
            try equal(ranges.count, Settings.specs.count, "setting count")
            for (row, spec) in zip(ranges, Settings.specs) {
                try equal(spec.name, row[0], "name"); try equal(spec.label, row[1], "label")
                try equal(spec.isBool ? dotBool(spec.minimum != 0) : String(spec.minimum), row[2], "minimum")
                try equal(spec.isBool ? dotBool(spec.maximum != 0) : String(spec.maximum), row[3], "maximum")
                try equal(Settings().raw(spec.name), row[4], "default"); referenceCases += 1
            }
        }
        let telemetryFiles = try f.files("telemetry")
        f.counts["telemetry"] = telemetryFiles.count
        try require(!telemetryFiles.isEmpty, "no telemetry reference files")
        for file in telemetryFiles {
            test("reference telemetry \(file)") {
                let expectedFile = try f.read(file)
                let lines = expectedFile.components(separatedBy: "\n")
                let seed = Int(lines[0].dropFirst(7))!
                let settings = Settings(line: String(lines[1].dropFirst(11)))
                let text = unescape(String(lines[2].dropFirst(7)))
                let runtime = VirtualRuntime(seed: seed)
                let path = directory.appendingPathComponent(URL(fileURLWithPath: file).lastPathComponent)
                var o = TypingOptions(settings: settings); o.telemetryPath = path.path
                try TypingEngine(runtime).type(text, options: o)
                let generated = try String(contentsOf: path, encoding: .utf8).components(separatedBy: "\n").enumerated().map { i, line -> String in
                    if i == 0 || line.isEmpty { return line }
                    var c = line.components(separatedBy: "\t"); c[1] = "T"; c[2] = "E"; if !c[3].isEmpty { c[3] = "G" }
                    return c.joined(separator: "\t")
                }.joined(separator: "\n")
                try equal(escape(units(generated)), escape(units(lines.dropFirst(3).joined(separator: "\n"))), "normalized telemetry"); referenceCases += 1
            }
        }
        test("data directory rejects relative overrides") {
            do { _ = try dataDirectory(environment: ["AUTOWRITE_DATA_DIR": "relative"]); throw TestFailure(description: "accepted relative directory") }
            catch AutoWriteError.message { }
            try equal(try dataDirectory(environment: ["AUTOWRITE_DATA_DIR": directory.path]).path, directory.path, "absolute override")
        }
    }
}

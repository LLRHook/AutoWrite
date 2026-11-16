import Foundation

extension TestRunner {
    func calibration(_ f: Fixtures, _ directory: URL) throws {
        test("reference capture sessions: recording rules and saved files") {
            let lines = try f.read("capture-sessions.txt").components(separatedBy: "\n")
            let sections = lines.joined(separator: "\n").components(separatedBy: "== ").dropFirst()
            f.counts["capture-sessions"] = sections.count
            for section in sections {
                let rows = section.components(separatedBy: "\n").dropFirst().filter { !$0.isEmpty }.map { $0.components(separatedBy: "\t") }
                let seconds = Int(rows.first { $0[0] == "in" && $0[1] == "seconds" }![2])!
                let current = Settings(line: rows.first { $0[0] == "in" && $0[1] == "settings" }![2])
                let capture = CaptureSession(seconds: seconds, current: current)
                var returns = [String]()
                for row in rows where row[0] == "in" {
                    if row[1] == "accept" { returns.append(dotBool(capture.accept(Unit(row[2])!, Double(row[3])!))) }
                    if row[1] == "ignore" { capture.ignore(row[2], Double(row[3])!) }
                }
                func out(_ name: String) -> String { rows.first { $0[0] == "out" && $0[1] == name }![2] }
                try equal(returns.joined(separator: " "), out("returns"), "returns")
                try equal(escape(capture.typed), out("typed"), "typed")
                try equal(String(capture.ignoredKeys), out("ignored"), "ignored count")
                try equal(dotBool(capture.started), out("started"), "started")
                try equal(dotBool(capture.finished), out("finished"), "finished")
                let expectedKeys = rows.filter { $0[0] == "out" && $0[1] == "key" }
                try equal(capture.keys.count, expectedKeys.count, "keys")
                for (key, row) in zip(capture.keys, expectedKeys) { try equal(key.milliseconds, Double(row[2])!, "key time"); try equal(key.character, Unit(row[3])!, "key unit") }
                let path = try capture.save(completed: true, directory: directory)
                let saved = try String(contentsOf: path, encoding: .utf8).components(separatedBy: "\n").dropLast().map { escape(units($0)) }
                try equal(saved, rows.filter { $0[0] == "out" && $0[1] == "saved" }.map { $0[2] }, "recording file")
                referenceCases += 1
            }
        }
        for (i, row) in try f.rows("alignment.tsv").enumerated() {
            test("reference alignment \(i)") {
                let actual = CalibrationAnalysis.align(unescape(row[0]), calibrationPassage)
                try equal(actual.end, Int(row[1])!, "end"); try equal(actual.distance, Int(row[2])!, "distance")
                try equal(actual.source.map(String.init).joined(separator: " "), row[3], "UTF16 source map"); referenceCases += 1
            }
        }
        let cases = try f.rows("analysis/index.tsv")
        for row in cases {
            let id = row[0]
            test("reference analysis \(id): numbers, text and all 26 proposals") {
                let capture = try readCapture(f, "analysis/\(id).capture.tsv")
                let result = CalibrationAnalysis.analyze(capture.keys, seconds: capture.seconds, current: capture.current)
                let expected = try f.rows("analysis/\(id).expected.tsv")
                let numbers: [String: Double] = ["measuredWpm":result.measuredWpm, "predictedWpm":result.predictedWpm, "accuracy":result.accuracy, "minimumWpm":result.minimumWpm, "maximumWpm":result.maximumWpm, "baseDelay":result.baseDelay, "ordinaryGap":result.ordinaryGap, "predictedOrdinaryGap":result.predictedOrdinaryGap, "ordinaryGapCount":Double(result.ordinaryGapCount), "correctedMistakes":Double(result.correctedMistakes), "alignedCharacters":Double(result.alignedCharacters)]
                var estimateIndex = 0
                for value in expected {
                    if let actual = numbers[value[0]] {
                        let number = Double(value[1])!
                        try require(abs(actual - number) <= 1e-9 * max(1, abs(number)), "\(value[0]): expected \(number), got \(actual)")
                    } else if value[0] == "summary" { try equal(escape(units(result.summary)), value[1], "summary") }
                    else if value[0] == "warning" { try equal(escape(units(result.warning)), value[1], "warning") }
                    else if value[0] == "activity" { try equal(escape(units(result.activityDescription)), value[1], "activity") }
                    else if value[0] == "proposed" { try equal(result.proposed.line, value[1], "proposed settings") }
                    else if value[0] == "estimate" {
                        let estimate = result.estimates[estimateIndex]; estimateIndex += 1
                        let actual = ["estimate", estimate.setting, Settings.spec(estimate.setting).label, capture.current.formatted(estimate.setting), result.proposed.formatted(estimate.setting), dotBool(estimate.measured), dotBool(estimate.closestAchievable), escape(units(estimate.reason))]
                        try equal(actual, value, "estimate \(estimate.setting)")
                    } else { throw TestFailure(description: "unrecognized result row \(value[0])") }
                }
                try equal(estimateIndex, 26, "estimate count"); referenceCases += 1
            }
        }
        print("REFERENCE calibration data: \(f.counts["capture-sessions"] ?? 0) capture sessions, \(f.counts["alignment"] ?? 0) alignments, \(cases.count) analysis cases")
    }
}
func readCapture(_ f: Fixtures, _ path: String) throws -> CaptureSession {
    let lines = try f.read(path).components(separatedBy: "\n")
    let header = lines[0].components(separatedBy: ";")
    let seconds = Int(header.first { $0.hasPrefix("seconds=") }!.dropFirst(8))!
    var settings = Settings()
    for line in lines where line.hasPrefix("# setting\t") {
        let parts = line.components(separatedBy: "\t"), spec = Settings.spec(parts[1])
        settings[parts[1]] = spec.isBool ? (parts[2] == "True" ? 1 : 0) : Int(parts[2])!
    }
    let capture = CaptureSession(seconds: seconds, current: settings)
    for line in lines where !line.isEmpty && !line.hasPrefix("#") && !line.hasPrefix("elapsed_ms") {
        let parts = line.components(separatedBy: "\t")
        if parts[1] == "character" { capture.keys.append(CaptureKey(milliseconds: Double(parts[0])!, character: Unit(parts[2])!)) }
    }
    return capture
}

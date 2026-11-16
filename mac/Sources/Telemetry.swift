import Foundation

final class Telemetry {
    private var handle: FileHandle?
    private let start = ProcessInfo.processInfo.systemUptime
    private var event = 0, lastKey = -1
    static func createPath(directory: URL) throws -> String {
        let folder = directory.appendingPathComponent("telemetry")
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        let formatter = DateFormatter(); formatter.locale = Locale(identifier: "en_US_POSIX"); formatter.dateFormat = "yyyyMMdd-HHmmss-SSS"
        return folder.appendingPathComponent("autowrite-" + formatter.string(from: Date()) + ".tsv").path
    }
    init(path: String, text: [Unit], options: TypingOptions) throws {
        if path == "-" { return }
        guard FileManager.default.createFile(atPath: path, contents: nil) else { throw AutoWriteError.message("Cannot create telemetry log.") }
        handle = try FileHandle(forWritingTo: URL(fileURLWithPath: path))
        write("event_id\tutc_iso\telapsed_ms\tgap_ms\tevent\tsource_index\tintended_code\tintended_text\tsent_code\tsent_text\tdelay_ms\tnote\n")
        let fields = [("wpm","WordsPerMinute"),("keep_target_wpm","KeepTargetWpm"),("mistake_rate_percent","TypoChancePercent"),("number_delay_penalty_percent","NumberDelayPenaltyPercent"),("punctuation_delay_penalty_percent","PunctuationDelayPenaltyPercent"),("shift_symbol_delay_penalty_percent","ShiftSymbolDelayPenaltyPercent"),("number_typo_multiplier_percent","NumberTypoMultiplierPercent"),("punctuation_typo_multiplier_percent","PunctuationTypoMultiplierPercent"),("shift_symbol_typo_multiplier_percent","ShiftSymbolTypoMultiplierPercent"),("paragraph_pause_ms","ParagraphPauseMilliseconds"),("notice_ms","CorrectionNoticeMilliseconds"),("detect_chars","CorrectionDetectChars"),("repair_ms","CorrectionPauseMilliseconds"),("resume_ms","CorrectionResumeMilliseconds")]
        info("run_start", "length=\(text.count);" + fields.map { "\($0.0)=\(options.settings.raw($0.1))" }.joined(separator: ";"))
    }
    private func write(_ text: String) { handle?.write(Data(text.utf8)) }
    static func escape(_ text: String) -> String { text.replacingOccurrences(of: "\\", with: "\\\\").replacingOccurrences(of: "\t", with: "\\t").replacingOccurrences(of: "\r", with: "\\r").replacingOccurrences(of: "\n", with: "\\n") }
    static func charText(_ c: Unit) -> String {
        switch c { case 0: return ""; case 10: return "<enter>"; case 13: return "<cr>"; case 8: return "<backspace>"; case 9: return "<tab>"; case 32: return "<space>"; default: return string([c]) }
    }
    func info(_ name: String, _ note: String) { record(name, -1, 0, 0, nil, note) }
    func pause(_ name: String, _ index: Int, _ delay: Int, _ note: String) { record(name, index, 0, 0, delay, note) }
    func key(_ name: String, _ index: Int, _ intended: Unit, _ sent: Unit, _ note: String) {
        record(name, index, intended, sent, nil, note); lastKey = Int((ProcessInfo.processInfo.systemUptime - start) * 1000)
    }
    private func record(_ name: String, _ index: Int, _ intended: Unit, _ sent: Unit, _ delay: Int?, _ note: String) {
        guard handle != nil else { return }
        event += 1
        let elapsed = Int((ProcessInfo.processInfo.systemUptime - start) * 1000)
        let date = ISO8601DateFormatter(); date.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        func code(_ c: Unit) -> String { c == 0 ? "" : String(format: "U+%04X", Int(c)) }
        let columns = [String(event), date.string(from: Date()), String(elapsed), sent != 0 && lastKey >= 0 ? String(elapsed - lastKey) : "", Self.escape(name), String(index), code(intended), Self.escape(Self.charText(intended)), code(sent), Self.escape(Self.charText(sent)), delay.map(String.init) ?? "", Self.escape(note)]
        write(columns.joined(separator: "\t") + "\n")
    }
    func close() { try? handle?.close(); handle = nil }
    deinit { close() }
}

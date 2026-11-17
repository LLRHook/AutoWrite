import Foundation

// Stored order is part of the Windows file and calibration-results contracts.
struct SettingSpec {
    let name: String, label: String
    let minimum: Int, maximum: Int, defaultValue: Int
    let isBool: Bool
    var isMultiplier: Bool { name.contains("Multiplier") }
    var storageKey: String { name.prefix(1).lowercased() + name.dropFirst() }
    func clamp(_ value: Int) -> Int { max(minimum, min(maximum, value)) }
}

struct Settings: Equatable {
    static let specs: [SettingSpec] = [
        SettingSpec(name: "WordsPerMinute", label: "Target WPM", minimum: 5, maximum: 180, defaultValue: 80, isBool: false),
        SettingSpec(name: "JitterPercent", label: "Jitter %", minimum: 0, maximum: 100, defaultValue: 55, isBool: false),
        SettingSpec(name: "KeyPairRhythmPercent", label: "Key-pair rhythm %", minimum: 0, maximum: 100, defaultValue: 80, isBool: false),
        SettingSpec(name: "BurstPercent", label: "Burst drift %", minimum: 0, maximum: 100, defaultValue: 30, isBool: false),
        SettingSpec(name: "WordPauseMilliseconds", label: "Word pause ms", minimum: 0, maximum: 1000, defaultValue: 120, isBool: false),
        SettingSpec(name: "SentencePauseMilliseconds", label: "Sentence pause ms", minimum: 0, maximum: 3000, defaultValue: 850, isBool: false),
        SettingSpec(name: "ParagraphPauseMilliseconds", label: "Paragraph pause ms", minimum: 0, maximum: 3000, defaultValue: 0, isBool: false),
        SettingSpec(name: "ThinkingPauseChancePercent", label: "Thinking pause %", minimum: 0, maximum: 25, defaultValue: 4, isBool: false),
        SettingSpec(name: "ThinkingPauseMilliseconds", label: "Thinking max ms", minimum: 0, maximum: 5000, defaultValue: 2200, isBool: false),
        SettingSpec(name: "KeepTargetWpm", label: "Keep target WPM", minimum: 0, maximum: 1, defaultValue: 1, isBool: true),
        SettingSpec(name: "UppercaseDelayPenaltyPercent", label: "Uppercase delay %", minimum: 0, maximum: 200, defaultValue: 10, isBool: false),
        SettingSpec(name: "NumberDelayPenaltyPercent", label: "Number delay %", minimum: 0, maximum: 200, defaultValue: 25, isBool: false),
        SettingSpec(name: "PunctuationDelayPenaltyPercent", label: "Punctuation delay %", minimum: 0, maximum: 200, defaultValue: 30, isBool: false),
        SettingSpec(name: "ShiftSymbolDelayPenaltyPercent", label: "Shift-symbol delay %", minimum: 0, maximum: 300, defaultValue: 55, isBool: false),
        SettingSpec(name: "NumberTypoMultiplierPercent", label: "Number mistakes x", minimum: 0, maximum: 500, defaultValue: 175, isBool: false),
        SettingSpec(name: "PunctuationTypoMultiplierPercent", label: "Punct. mistakes x", minimum: 0, maximum: 500, defaultValue: 200, isBool: false),
        SettingSpec(name: "ShiftSymbolTypoMultiplierPercent", label: "Shift mistakes x", minimum: 0, maximum: 500, defaultValue: 300, isBool: false),
        SettingSpec(name: "EnableTypos", label: "Corrected mistakes", minimum: 0, maximum: 1, defaultValue: 1, isBool: true),
        SettingSpec(name: "TypoChancePercent", label: "Mistake rate %", minimum: 0, maximum: 10, defaultValue: 1, isBool: false),
        SettingSpec(name: "DuplicateChancePercent", label: "Duplicate share %", minimum: 0, maximum: 100, defaultValue: 0, isBool: false),
        SettingSpec(name: "TransposeChancePercent", label: "Transpose share %", minimum: 0, maximum: 100, defaultValue: 0, isBool: false),
        SettingSpec(name: "CorrectionNoticeMilliseconds", label: "Notice delay ms", minimum: 0, maximum: 3000, defaultValue: 850, isBool: false),
        SettingSpec(name: "CorrectionDetectChars", label: "Detect after chars", minimum: 0, maximum: 8, defaultValue: 3, isBool: false),
        SettingSpec(name: "CorrectionPauseMilliseconds", label: "Repair key pause ms", minimum: 0, maximum: 1000, defaultValue: 120, isBool: false),
        SettingSpec(name: "CorrectionResumeMilliseconds", label: "Resume pause ms", minimum: 0, maximum: 2000, defaultValue: 260, isBool: false),
        SettingSpec(name: "ButtonDelaySeconds", label: "Button delay s", minimum: 0, maximum: 10, defaultValue: 3, isBool: false),
    ]
    private var values = Dictionary(uniqueKeysWithValues: specs.map { ($0.name, $0.defaultValue) })
    subscript(_ name: String) -> Int {
        get { values[name]! }
        set { values[name] = newValue }
    }
    static func spec(_ name: String) -> SettingSpec { specs.first { $0.name == name }! }
    func raw(_ name: String) -> String { Self.spec(name).isBool ? dotBool(self[name] != 0) : String(self[name]) }
    func formatted(_ name: String) -> String {
        let spec = Self.spec(name)
        if spec.isBool { return self[name] != 0 ? "On" : "Off" }
        if spec.isMultiplier { return fixed(Double(self[name]) / 100, 2) + "x" }
        return String(self[name])
    }
    var line: String { Self.specs.map { "\($0.name)=\(raw($0.name))" }.joined(separator: ";") }
    init() {}
    init(line: String) {
        for entry in line.split(separator: ";") {
            let pair = entry.split(separator: "=", maxSplits: 1).map(String.init)
            guard pair.count == 2, let spec = Self.specs.first(where: { $0.name == pair[0] }) else { continue }
            self[pair[0]] = spec.isBool ? (pair[1] == "True" ? 1 : 0) : Int(pair[1])!
        }
    }
    var fileText: String { Self.specs.map { "\($0.storageKey)=\(raw($0.name))\n" }.joined() }
    static func loadText(_ input: String) -> Settings {
        var settings = Settings()
        var input = input
        if input.hasPrefix("\u{FEFF}") { input.removeFirst() }
        for raw in input.components(separatedBy: CharacterSet(charactersIn: "\r\n")) {
            let line = trim(raw)
            guard !line.isEmpty, !line.hasPrefix("#"), let eq = line.firstIndex(of: "="), eq != line.startIndex else { continue }
            let key = asciiLower(trim(String(line[..<eq]))), value = trim(String(line[line.index(after: eq)...]))
            if key == "characterspersecond" {
                if let cps = parseInt(value), cps > 0, cps != 15 {
                    settings["WordsPerMinute"] = spec("WordsPerMinute").clamp(int32Cast((Double(cps) * 60 / 5).rounded(.toNearestOrEven)))
                }
                continue
            }
            let mapped = key == "variancemilliseconds" ? "jitterpercent" : key
            guard let spec = specs.first(where: { asciiLower($0.storageKey) == mapped }) else { continue }
            if spec.isBool {
                if let parsed = parseBool(value) { settings[spec.name] = parsed ? 1 : 0 }
            } else if let number = parseInt(value) { settings[spec.name] = number }
        }
        for spec in specs { settings[spec.name] = spec.clamp(settings[spec.name]) }
        return settings
    }
    static func load(directory: URL) throws -> Settings {
        let file = directory.appendingPathComponent("settings.ini")
        if !FileManager.default.fileExists(atPath: file.path) { return Settings() }
        let bytes = Array(try Data(contentsOf: file))
        if bytes.starts(with: [0xFF, 0xFE]) || bytes.starts(with: [0xFE, 0xFF]) {
            let little = bytes[0] == 0xFF
            var codeUnits = [Unit](), i = 2
            while i + 1 < bytes.count {
                codeUnits.append(little ? Unit(bytes[i]) | Unit(bytes[i + 1]) << 8 : Unit(bytes[i]) << 8 | Unit(bytes[i + 1])); i += 2
            }
            if i < bytes.count { codeUnits.append(0xFFFD) }
            return loadText(string(codeUnits))
        }
        return loadText(String(decoding: bytes, as: UTF8.self))
    }
    @discardableResult func save(directory: URL) -> Bool {
        do {
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
            try fileText.write(to: directory.appendingPathComponent("settings.ini"), atomically: true, encoding: .utf8)
            return true
        } catch { return false }
    }
}

func dataDirectory(environment: [String: String] = ProcessInfo.processInfo.environment) throws -> URL {
    if let override = environment["AUTOWRITE_DATA_DIR"], !override.isEmpty {
        guard override.hasPrefix("/") else { throw AutoWriteError.message("AUTOWRITE_DATA_DIR must be absolute.") }
        return URL(fileURLWithPath: override, isDirectory: true).standardizedFileURL
    }
    return FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Library/Application Support/AutoWrite", isDirectory: true)
}

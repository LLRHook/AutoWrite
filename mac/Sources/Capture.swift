import Foundation

struct CaptureKey { let milliseconds: Double; let character: Unit }
final class CaptureSession {
    var keys = [CaptureKey](), raw = [String]()
    var seconds: Int, ignoredKeys = 0, started = false, finished = false
    var typed = [Unit]()
    let current: Settings
    init(seconds: Int, current: Settings = Settings()) { self.seconds = seconds; self.current = current }
    @discardableResult func accept(_ character: Unit, _ ms: Double) -> Bool {
        if finished { return false }
        if ms >= Double(seconds) * 1000 && started { finished = true; return false }
        let key: Unit = character == 13 ? 10 : character
        if key != 8 && key != 10 && (key < 32 || key > 126) { ignore("character \(key)", ms); return false }
        started = true; keys.append(CaptureKey(milliseconds: ms, character: key))
        raw.append(fixed(ms, 3) + "\tcharacter\t\(key)")
        if key == 8 { if !typed.isEmpty { typed.removeLast() } } else { typed.append(key) }
        return true
    }
    func ignore(_ reason: String, _ ms: Double) { ignoredKeys += 1; raw.append(fixed(ms, 3) + "\tignored\t" + reason) }
    func fileText(completed: Bool) -> String {
        (["# version=1;seconds=\(seconds);completed=\(dotBool(completed));ignored_keys=\(ignoredKeys)"] + Settings.specs.map { "# setting\t\($0.name)\t\(current.raw($0.name))" } + ["elapsed_ms\tkind\tcharacter_code_or_reason"] + raw).joined(separator: "\n") + "\n"
    }
    @discardableResult func save(completed: Bool, directory: URL) throws -> URL {
        let folder = directory.appendingPathComponent("calibration")
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        let date = DateFormatter(); date.locale = Locale(identifier: "en_US_POSIX"); date.timeZone = TimeZone(secondsFromGMT: 0); date.dateFormat = "yyyyMMdd-HHmmss-SSS"
        let path = folder.appendingPathComponent("capture-" + date.string(from: Date()) + "-" + UUID().uuidString.replacingOccurrences(of: "-", with: "").lowercased().prefix(8) + ".tsv")
        try fileText(completed: completed).write(to: path, atomically: true, encoding: .utf8)
        return path
    }
}

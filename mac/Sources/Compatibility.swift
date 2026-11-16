import Foundation

typealias Unit = UInt16
func units(_ text: String) -> [Unit] { Array(text.utf16) }
func string(_ text: [Unit]) -> String { String(decoding: text, as: UTF16.self) }
func dotBool(_ value: Bool) -> String { value ? "True" : "False" }
func roundEven(_ value: Double) -> Int { Int(value.rounded(.toNearestOrEven)) }
func trim(_ text: String) -> String { text.trimmingCharacters(in: .whitespacesAndNewlines) }
func parseInt(_ text: String) -> Int? {
    let chars = Array(trim(text).utf8)
    guard !chars.isEmpty else { return nil }
    let start = chars[0] == 43 || chars[0] == 45 ? 1 : 0
    guard start < chars.count, chars.dropFirst(start).allSatisfy({ $0 >= 48 && $0 <= 57 }), let value = Int32(trim(text)) else { return nil }
    return Int(value)
}

// Framework F-format first reduces to 15 significant decimal digits, then
// rounds ties away from zero. Swift/printf fixed formatting uses different ties.
func fixed(_ value: Double, _ places: Int) -> String {
    if !value.isFinite { return value.isNaN ? "NaN" : value > 0 ? "Infinity" : "-Infinity" }
    let significant = String(format: "%.14e", locale: Locale(identifier: "en_US_POSIX"), value)
    var decimal = Decimal(string: significant, locale: Locale(identifier: "en_US_POSIX"))!
    var rounded = Decimal()
    NSDecimalRound(&rounded, &decimal, places, .plain)
    var text = NSDecimalNumber(decimal: rounded).stringValue
    if !text.contains(".") { text += "." }
    let count = text.split(separator: ".", omittingEmptySubsequences: false).last!.count
    text += String(repeating: "0", count: max(0, places - count))
    return text
}

enum AutoWriteError: Error, CustomStringConvertible {
    case cancelled
    case message(String)
    var description: String {
        switch self { case .cancelled: return "Stopped"; case .message(let text): return text }
    }
}

// .NET Framework's seeded subtractive generator. Int64 intermediates preserve
// the unchecked Int32 semantics used by its initialization at extreme seeds.
final class DotRandom {
    private var seedArray = [Int](repeating: 0, count: 56)
    private var inext = 0, inextp = 21
    init(_ seed: Int) {
        let subtraction = seed == -2147483648 ? 2147483647 : abs(seed)
        var mj = 161803398 - subtraction
        seedArray[55] = mj
        var mk = 1
        for i in 1..<55 {
            let ii = (21 * i) % 55
            seedArray[ii] = mk
            mk = mj - mk
            if mk < 0 { mk += 2147483647 }
            mj = seedArray[ii]
        }
        for _ in 1...4 {
            for i in 1...55 {
                seedArray[i] = Int(Int32(truncatingIfNeeded: seedArray[i] - seedArray[1 + (i + 30) % 55]))
                if seedArray[i] < 0 { seedArray[i] += 2147483647 }
            }
        }
    }
    func next() -> Int {
        inext += 1; if inext >= 56 { inext = 1 }
        inextp += 1; if inextp >= 56 { inextp = 1 }
        var value = seedArray[inext] - seedArray[inextp]
        if value == 2147483647 { value -= 1 }
        if value < 0 { value += 2147483647 }
        seedArray[inext] = value
        return value
    }
    func double() -> Double { Double(next()) * (1.0 / 2147483647.0) }
    func next(_ max: Int) -> Int { Int(double() * Double(max)) }
    func next(_ min: Int, _ max: Int) -> Int { next(max - min) + min }
}

enum DotChar {
    static func scalar(_ c: Unit) -> Unicode.Scalar? { Unicode.Scalar(UInt32(c)) }
    static func whitespace(_ c: Unit) -> Bool {
        guard let s = scalar(c) else { return false }
        return s.properties.isWhitespace
    }
    static func category(_ c: Unit) -> Unicode.GeneralCategory? { scalar(c)?.properties.generalCategory }
    static func control(_ c: Unit) -> Bool { category(c) == .control }
    static func upper(_ c: Unit) -> Bool { category(c) == .uppercaseLetter }
    static func digit(_ c: Unit) -> Bool { category(c) == .decimalNumber }
    static func letterDigit(_ c: Unit) -> Bool {
        switch category(c) {
        case .uppercaseLetter, .lowercaseLetter, .titlecaseLetter, .modifierLetter, .otherLetter, .decimalNumber: return true
        default: return false
        }
    }
    static func punctuationSymbol(_ c: Unit) -> Bool {
        switch category(c) {
        case .connectorPunctuation, .dashPunctuation, .openPunctuation, .closePunctuation, .initialPunctuation, .finalPunctuation, .otherPunctuation,
             .mathSymbol, .currencySymbol, .modifierSymbol, .otherSymbol: return true
        default: return false
        }
    }
    static func lower(_ c: Unit) -> Unit {
        guard let s = scalar(c) else { return c }
        let mapping = Array(s.properties.lowercaseMapping.utf16)
        return mapping.count == 1 ? mapping[0] : c
    }
    static func toUpper(_ c: Unit) -> Unit {
        guard let s = scalar(c) else { return c }
        let mapping = Array(s.properties.uppercaseMapping.utf16)
        return mapping.count == 1 ? mapping[0] : c
    }
}

import Foundation

typealias Unit = UInt16
func units(_ text: String) -> [Unit] { Array(text.utf16) }
func string(_ text: [Unit]) -> String { String(decoding: text, as: UTF16.self) }
func dotBool(_ value: Bool) -> String { value ? "True" : "False" }
func roundEven(_ value: Double) -> Int { Int(value.rounded(.toNearestOrEven)) }
func trim(_ text: String) -> String {
    let chars = units(text)
    var first = 0, last = chars.count
    while first < last && DotChar.whitespace(chars[first]) { first += 1 }
    while last > first && DotChar.whitespace(chars[last - 1]) { last -= 1 }
    return string(Array(chars[first..<last]))
}
func asciiLower(_ text: String) -> String { string(units(text).map { (65...90).contains($0) ? $0 + 32 : $0 }) }
func parseInt(_ text: String) -> Int? {
    var input = units(text)
    while input.last == 0 { input.removeLast() }
    let chars = Array(trim(string(input)).utf8)
    guard !chars.isEmpty else { return nil }
    let start = chars[0] == 43 || chars[0] == 45 ? 1 : 0
    guard start < chars.count, chars.dropFirst(start).allSatisfy({ $0 >= 48 && $0 <= 57 }), let value = Int32(String(decoding: chars, as: UTF8.self)) else { return nil }
    return Int(value)
}
func parseBool(_ text: String) -> Bool? {
    var chars = units(text)
    while let c = chars.first, c == 0 || DotChar.whitespace(c) { chars.removeFirst() }
    while let c = chars.last, c == 0 || DotChar.whitespace(c) { chars.removeLast() }
    switch asciiLower(string(chars)) { case "true": return true; case "false": return false; default: return nil }
}
func int32Cast(_ value: Double) -> Int {
    value.isFinite && value >= Double(Int32.min) && value < Double(Int32.max) + 1 ? Int(value) : Int(Int32.min)
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
        switch s.properties.generalCategory {
        case .spaceSeparator, .lineSeparator, .paragraphSeparator: return true
        default: return (9...13).contains(c) || c == 0x85
        }
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

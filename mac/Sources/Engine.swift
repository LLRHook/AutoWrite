import Foundation

struct TypingOptions {
    var settings = Settings()
    var startDelay = 0
    var triggerKey = 0
    var telemetryPath = "-"
    subscript(_ name: String) -> Int { settings[name] }
}

protocol EngineRuntime: AnyObject {
    func createRandom() -> DotRandom
    func checkCancellation() throws
    func foregroundWindow() throws -> UInt64
    func isKeyDown(_ key: Int) throws -> Bool
    func releaseModifiers() throws
    func sendCharacter(_ value: Unit) throws
    func sleep(_ milliseconds: Int) throws
    var milliseconds: Double { get }
}

extension EngineRuntime { func checkCancellation() throws {} }

final class VirtualRuntime: EngineRuntime {
    let seed: Int
    var time = 0
    var limit = Int.max
    var keys = [CaptureKey]()
    var onSend: ((Unit, Int) -> Void)?
    init(seed: Int, limit: Int = Int.max) { self.seed = seed; self.limit = limit }
    func createRandom() -> DotRandom { DotRandom(seed) }
    func foregroundWindow() throws -> UInt64 { 0 }
    func isKeyDown(_ key: Int) throws -> Bool { false }
    func releaseModifiers() throws {}
    func sendCharacter(_ value: Unit) throws { keys.append(CaptureKey(milliseconds: Double(time), character: value)); onSend?(value, time) }
    func sleep(_ milliseconds: Int) throws {
        time += max(0, milliseconds)
        if time > limit { throw AutoWriteError.cancelled }
    }
    var milliseconds: Double { Double(time) }
    var log: String { keys.map { "\(Int($0.milliseconds)):\($0.character);" }.joined() }
}

final class TypingEngine {
    let runtime: EngineRuntime
    private var nearby = [Unit: [Unit]](), hands = [Unit: Int]()
    init(_ runtime: EngineRuntime) {
        self.runtime = runtime
        let neighbors = ["a":"qwsz", "b":"vghn", "c":"xdfv", "d":"serfcx", "e":"wsdr", "f":"drtgvc", "g":"ftyhbv", "h":"gyujnb", "i":"ujko", "j":"huikmn", "k":"jiolm", "l":"kop", "m":"njk", "n":"bhjm", "o":"iklp", "p":"ol", "q":"wa", "r":"edft", "s":"awedxz", "t":"rfgy", "u":"yhji", "v":"cfgb", "w":"qase", "x":"zsdc", "y":"tghu", "z":"asx", "1":"2q", "2":"13qw", "3":"24we", "4":"35er", "5":"46rt", "6":"57ty", "7":"68yu", "8":"79ui", "9":"80io", "0":"9op", "-":"0=[]", "=":"-[]", "[":"p=]", "]":"[\\", "\\":"]", ";":"lp'", "'":";", ",":"m.", ".":",/", "/":".", "`":"1"]
        for (key, value) in neighbors { nearby[units(key)[0]] = units(value) }
        for c in units("qwertasdfgzxcvb12345`~!@#$%") { hands[c] = 0 }
        for c in units("yuiophjklnm67890^&*()_-+={[}]|\\:;\"'<,>.?/") { hands[c] = 1 }
    }
    enum Kind: String { case nearby = "Nearby", missedShift = "MissedShift", duplicate = "Duplicate", transpose = "Transpose" }
    struct Mistake { let kind: Kind, sent: Unit, detect: Int }
    struct Active { let mistake: Mistake, start: Int, extra: Int; var through: Int, typed: Int }
    struct Rhythm { var remaining = 0; var factor = 0.0; var thinking = [Int: Int]() }
    static let shifted = units("~!@#$%^&*()_+{}|:\"<>?")
    static let plain = units("`1234567890-=[]\\;',./")
    static func missedShift(_ c: Unit) -> Unit? { shifted.firstIndex(of: c).map { plain[$0] } }
    static func sentence(_ c: Unit) -> Bool { c == 46 || c == 33 || c == 63 }
    static func boundary(_ c: Unit) -> Bool { DotChar.whitespace(c) || units(",;:.!?").contains(c) }
    static func eligible(_ c: Unit) -> Bool {
        !DotChar.whitespace(c) && !DotChar.control(c) && (DotChar.letterDigit(c) || DotChar.punctuationSymbol(c))
    }
    static func characterFactor(_ c: Unit, _ o: TypingOptions) -> Double {
        if c == 0 || DotChar.control(c) { return 1 }
        let key: String
        if shifted.contains(c) { key = "ShiftSymbolDelayPenaltyPercent" }
        else if DotChar.upper(c) { key = "UppercaseDelayPenaltyPercent" }
        else if DotChar.digit(c) { key = "NumberDelayPenaltyPercent" }
        else if DotChar.punctuationSymbol(c) { key = "PunctuationDelayPenaltyPercent" }
        else { return 1 }
        return 1 + Double(max(0, o[key])) / 100
    }
    static func mistakeWeight(_ c: Unit, _ o: TypingOptions) -> Double {
        guard eligible(c) else { return 0 }
        if shifted.contains(c) { return Double(max(0, o["ShiftSymbolTypoMultiplierPercent"])) / 100 }
        if DotChar.digit(c) { return Double(max(0, o["NumberTypoMultiplierPercent"])) / 100 }
        if DotChar.punctuationSymbol(c) { return Double(max(0, o["PunctuationTypoMultiplierPercent"])) / 100 }
        return 1
    }
    func detection(_ o: TypingOptions, _ r: DotRandom) -> Int {
        let maximum = max(0, o["CorrectionDetectChars"])
        if maximum <= 0 { return 0 }
        let roll = r.double()
        if roll < 0.45 { return min(maximum, 1) }
        if roll < 0.75 { return min(maximum, 2) }
        if roll < 0.92 { return min(maximum, 3) }
        return maximum
    }
    func createMistake(_ text: [Unit], _ index: Int, _ o: TypingOptions, _ r: DotRandom) -> Mistake {
        var duplicate = max(0, min(100, o["DuplicateChancePercent"]))
        var transpose = max(0, min(100, o["TransposeChancePercent"]))
        if duplicate + transpose > 100 {
            let scale = 100.0 / Double(duplicate + transpose)
            duplicate = roundEven(Double(duplicate) * scale); transpose = max(0, 100 - duplicate)
        }
        let nearbyWeight = max(0, 100 - duplicate - transpose)
        let roll = r.next(max(1, nearbyWeight + duplicate + transpose))
        if roll < transpose && index + 1 < text.count && Self.eligible(text[index]) && Self.eligible(text[index + 1]) && DotChar.lower(text[index]) != DotChar.lower(text[index + 1]) {
            return Mistake(kind: .transpose, sent: 0, detect: detection(o, r))
        }
        if roll < transpose + duplicate { return Mistake(kind: .duplicate, sent: text[index], detect: detection(o, r)) }
        if let typo = Self.missedShift(text[index]) { return Mistake(kind: .missedShift, sent: typo, detect: detection(o, r)) }
        if let choices = nearby[DotChar.lower(text[index])], !choices.isEmpty {
            var typo = choices[r.next(choices.count)]
            if DotChar.upper(text[index]) { typo = DotChar.toUpper(typo) }
            return Mistake(kind: .nearby, sent: typo, detect: detection(o, r))
        }
        return Mistake(kind: .duplicate, sent: text[index], detect: detection(o, r))
    }
    func mistakePlan(_ text: [Unit], _ o: TypingOptions, _ r: DotRandom) throws -> [Int: Mistake] {
        if o["EnableTypos"] == 0 || o["TypoChancePercent"] <= 0 { return [:] }
        var candidates = [(Int, Double)](), totalWeight = 0.0
        for (index, c) in text.enumerated() {
            if index & 255 == 0 { try runtime.checkCancellation() }
            if !Self.eligible(c) { continue }
            let weight = Self.mistakeWeight(c, o)
            if weight > 0 { candidates.append((index, weight)); totalWeight += weight }
        }
        var target = roundEven(totalWeight * (Double(max(0, o["TypoChancePercent"])) / 100))
        target = max(o["TypoChancePercent"] > 0 && !candidates.isEmpty ? 1 : 0, target)
        target = min(target, candidates.count)
        var blocked = Set<Int>(), plan = [Int: Mistake]()
        while plan.count < target {
            try runtime.checkCancellation()
            var weight = 0.0
            for (offset, candidate) in candidates.enumerated() {
                if offset & 255 == 0 { try runtime.checkCancellation() }
                if !blocked.contains(candidate.0) { weight += max(0, candidate.1) }
            }
            if weight <= 0 { break }
            var roll = r.double() * weight, selected: Int?
            for (offset, candidate) in candidates.enumerated() {
                if offset & 255 == 0 { try runtime.checkCancellation() }
                let (index, value) = candidate
                if blocked.contains(index) { continue }
                roll -= max(0, value)
                if roll <= 0 { selected = index; break }
            }
            guard let index = selected else { break }
            let mistake = createMistake(text, index, o, r)
            plan[index] = mistake
            blocked.formUnion([index - 1, index, index + 1])
            if mistake.kind == .transpose { blocked.insert(index + 2) }
        }
        return plan
    }
    func rhythmPlan(_ text: [Unit], _ o: TypingOptions, _ r: DotRandom) throws -> [Int: Int] {
        if o["ThinkingPauseChancePercent"] <= 0 || o["ThinkingPauseMilliseconds"] <= 0 { return [:] }
        var candidates = [Int]()
        for index in text.indices {
            if index & 255 == 0 { try runtime.checkCancellation() }
            if Self.boundary(text[index]) { candidates.append(index) }
        }
        var target = roundEven(Double(candidates.count) * (Double(o["ThinkingPauseChancePercent"]) / 100))
        if target <= 0 && !candidates.isEmpty && o["ThinkingPauseChancePercent"] > 0 { target = 1 }
        if candidates.count > 1 {
            for index in stride(from: candidates.count - 1, through: 1, by: -1) {
                if index & 255 == 0 { try runtime.checkCancellation() }
                candidates.swapAt(index, r.next(index + 1))
            }
        }
        var result = [Int: Int]()
        for index in candidates {
            if index & 255 == 0 { try runtime.checkCancellation() }
            if result.count >= target { break }
            result[index] = r.next(max(1, o["ThinkingPauseMilliseconds"] / 3), o["ThinkingPauseMilliseconds"] + 1)
        }
        return result
    }
    func baseDelay(_ text: [Unit], _ o: TypingOptions, _ count: Int, _ rhythm: [Int: Int]) throws -> Int {
        let target = Double(text.count) * 60000 / (Double(max(1, o["WordsPerMinute"])) * 5)
        var natural = 0.0
        for (index, current) in text.enumerated() {
            if index & 255 == 0 { try runtime.checkCancellation() }
            if Self.sentence(current) { natural += Double(o["SentencePauseMilliseconds"]) }
            else if DotChar.whitespace(current) { natural += Double(o["WordPauseMilliseconds"]) }
            if current == 10 || (current == 13 && (index + 1 >= text.count || text[index + 1] != 10)) { natural += Double(o["ParagraphPauseMilliseconds"]) }
            if let delay = rhythm[index] { natural += Double(delay) }
        }
        var correction = 0.0
        if count > 0 && o["EnableTypos"] != 0 {
            let averageDistance = min(Double(max(0, o["CorrectionDetectChars"])), 2.2)
            let repairs = 2 * (averageDistance + 1) + 0.25
            correction = Double(count) * (Double(o["CorrectionNoticeMilliseconds"]) + Double(o["CorrectionResumeMilliseconds"]) + (repairs * Double(max(1, o["CorrectionPauseMilliseconds"]))))
        }
        var available = target - natural - correction
        if available < Double(text.count) * 45 { available = Double(text.count) * 45 }
        var motorUnits = 0.0
        if o["KeepTargetWpm"] != 0 { for (index, c) in text.enumerated() { if index & 255 == 0 { try runtime.checkCancellation() }; motorUnits += Self.characterFactor(c, o) }; motorUnits = max(1, motorUnits) }
        else { motorUnits = max(1, Double(text.count)) }
        return max(45, min(220, roundEven(available / max(1, motorUnits)) - 5))
    }
    func variableDelay(_ center: Int, _ spread: Double, _ r: DotRandom) -> Int {
        if center <= 0 { return 0 }
        let factor = max(0.2, min(2.2, 1 + ((r.double() * 2 - 1) * spread)))
        return max(1, roundEven(Double(center) * factor))
    }
    func repairDelay(_ o: TypingOptions, _ r: DotRandom) -> Int { variableDelay(max(1, o["CorrectionPauseMilliseconds"]), 0.35, r) }
    func nextDelay(_ text: [Unit], _ index: Int, _ base: Int, _ o: TypingOptions, _ r: DotRandom, _ state: inout Rhythm) -> Int {
        let current = index >= 0 && index < text.count ? text[index] : 0, next: Unit = index + 1 < text.count ? text[index + 1] : 0
        var delay = Double(base)
        var factor = 1.0
        if o["KeyPairRhythmPercent"] > 0 && current != 0 && next != 0 {
            let amount = Double(max(0, min(100, o["KeyPairRhythmPercent"]))) / 100
            if DotChar.lower(current) == DotChar.lower(next) { factor = 1 + (0.45 * amount) }
            else if let a = hands[DotChar.lower(current)], let b = hands[DotChar.lower(next)] { factor = a == b ? 1 + (0.22 * amount) : 1 - (0.15 * amount) }
        }
        delay *= factor
        delay *= Self.characterFactor(next, o)
        if state.remaining <= 0 {
            state.remaining = r.next(8, 24)
            let range = Double(max(0, o["BurstPercent"])) / 100
            state.factor = 1 + ((r.double() * 2 - 1) * range)
            state.factor = max(0.25, min(2.5, state.factor))
        }
        state.remaining -= 1; delay *= state.factor
        if o["JitterPercent"] > 0 {
            let sigma = min(1, Double(o["JitterPercent"]) / 100 * 0.65)
            let u1 = 1 - r.double(), u2 = 1 - r.double()
            let normal = sqrt(-2 * log(u1)) * sin(2 * Double.pi * u2)
            delay *= exp((normal * sigma) - (sigma * sigma / 2))
        }
        if Self.sentence(current) { delay += Double(o["SentencePauseMilliseconds"]) * (0.65 + r.double() * 0.7) }
        else if DotChar.whitespace(current) { delay += Double(o["WordPauseMilliseconds"]) * (0.65 + r.double() * 0.7) }
        if o["ParagraphPauseMilliseconds"] > 0 && (current == 10 || current == 13) { delay += Double(o["ParagraphPauseMilliseconds"]) * (0.65 + r.double() * 0.7) }
        if let thinking = state.thinking[index] { delay += Double(thinking) }
        return max(1, roundEven(delay))
    }
    func type(_ text: [Unit], options o: TypingOptions, progress: ((Int, Int) -> Void)? = nil) throws {
        let telemetry = try Telemetry(path: o.telemetryPath, text: text, options: o)
        defer { telemetry.close() }
        func pause(_ ms: Int, _ name: String, _ index: Int, _ note: String) throws {
            if ms > 0 { telemetry.pause(name, index, ms, note) }
            try runtime.sleep(ms)
        }
        if o.startDelay > 0 { try pause(o.startDelay, "start_delay", -1, "button delay") }
        if o.triggerKey > 0 {
            let deadline = runtime.milliseconds + 1000
            while try runtime.isKeyDown(o.triggerKey) && runtime.milliseconds < deadline { try runtime.sleep(10) }
        }
        try runtime.releaseModifiers()
        let target = try runtime.foregroundWindow(), random = runtime.createRandom()
        let mistakes = try mistakePlan(text, o, random), thinking = try rhythmPlan(text, o, random)
        let base = try baseDelay(text, o, mistakes.count, thinking)
        telemetry.info("calibration", "base_delay_ms=\(base);planned_mistakes=\(mistakes.count);planned_thinking_pauses=\(thinking.count);target_wpm=\(o["WordsPerMinute"])")
        var state = Rhythm(); state.thinking = thinking
        var completed = 0, index = 0
        var active: Active?
        func send(_ value: Unit, _ source: Int, _ intended: Unit, _ action: String, _ note: String) throws {
            if target != 0 { while try runtime.foregroundWindow() != target { try runtime.sleep(50) } }
            try runtime.sendCharacter(value)
            telemetry.key(action, source, intended, value, note)
        }
        func correct(_ a: Active) throws {
            let through = max(a.start, min(a.through, text.count - 1))
            try pause(variableDelay(o["CorrectionNoticeMilliseconds"], 0.35, random), "notice_error", through, "kind=\(a.mistake.kind.rawValue);start=\(a.start);through=\(through)")
            let backspaces = through - a.start + 1 + a.extra
            for offset in 0..<backspaces {
                try send(8, through - offset, 8, "repair_backspace", "correction")
                try pause(repairDelay(o, random), "repair_delay", through, "after backspace")
            }
            for i in a.start...through {
                let c = text[i]
                try send(c == 13 || c == 10 ? 10 : c, i, c, "repair_retype", c == 13 || c == 10 ? "newline" : "correction")
                try pause(repairDelay(o, random), "repair_delay", i, "after retype")
            }
            try pause(variableDelay(o["CorrectionResumeMilliseconds"], 0.4, random), "resume_after_fix", through, "correction complete")
            telemetry.info("correction_done", "kind=\(a.mistake.kind.rawValue);start=\(a.start);through=\(through);backspaces=\(backspaces)")
        }
        while index < text.count {
            try runtime.sleep(0)
            let current = text[index]
            if current == 13 || current == 10 {
                if let a = active { try correct(a); active = nil }
                try send(10, index, current, "enter", "newline")
                if current == 13 && index + 1 < text.count && text[index + 1] == 10 { index += 1; completed += 1 }
                completed += 1; progress?(completed, text.count)
                try pause(nextDelay(text, index, base, o, random, &state), "motor_delay", index, "after newline")
                index += 1; continue
            }
            if active == nil, let mistake = mistakes[index] {
                if mistake.kind == .transpose && index + 1 < text.count {
                    let next = text[index + 1]
                    try send(next, index, current, "mistake_transpose", "sent next char first")
                    try pause(repairDelay(o, random), "mistake_key_gap", index, "transpose second key")
                    try send(current, index + 1, next, "mistake_transpose", "sent previous char second")
                    active = Active(mistake: mistake, start: index, extra: 0, through: index + 1, typed: 2)
                    index += 1; completed += 2
                } else if mistake.kind == .duplicate {
                    try send(current, index, current, "final_key", "before duplicate")
                    try pause(repairDelay(o, random), "mistake_key_gap", index, "duplicate extra key")
                    try send(current, index, current, "mistake_duplicate", "extra duplicate")
                    active = Active(mistake: mistake, start: index, extra: 1, through: index, typed: 1); completed += 1
                } else {
                    try send(mistake.sent, index, current, mistake.kind == .missedShift ? "mistake_shift" : "mistake_nearby", mistake.kind == .missedShift ? "missed shift" : "nearby substitution")
                    active = Active(mistake: mistake, start: index, extra: 0, through: index, typed: 1); completed += 1
                }
            } else {
                try send(current, index, current, "final_key", "normal")
                if active != nil { active!.through = index; active!.typed += 1 }
                completed += 1
            }
            progress?(completed, text.count)
            if let a = active, a.typed - 1 >= a.mistake.detect || (a.typed > 1 && Self.boundary(text[index])) { try correct(a); active = nil }
            try pause(nextDelay(text, index, base, o, random, &state), "motor_delay", index, "after key")
            index += 1
        }
        if let a = active { try correct(a) }
        telemetry.info("run_done", "completed=\(completed);total=\(text.count)")
    }
}

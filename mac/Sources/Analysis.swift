import Foundation

struct CalibrationEstimate {
    let setting: String, current: Int
    var proposed: Int, measured = false, closestAchievable = false, reason: String
}
final class CalibrationResult {
    var proposed: Settings
    var estimates = [CalibrationEstimate]()
    var measuredWpm = 0.0, accuracy = 0.0, predictedWpm = 0.0, minimumWpm = 0.0, maximumWpm = 0.0
    var warning = "", activityDescription = ""
    var correctedMistakes = 0, alignedCharacters = 0, ordinaryGapCount = 0
    var baseDelay = 0.0, ordinaryGap = 0.0, predictedOrdinaryGap = 0.0
    init(current: Settings) {
        proposed = current
        estimates = Settings.specs.map { CalibrationEstimate(setting: $0.name, current: current[$0.name], proposed: current[$0.name], reason: $0.name == "ButtonDelaySeconds" ? "Kept: start-button preference is unrelated to typing." : "Kept: insufficient independent samples.") }
    }
    var summary: String {
        "Measured speed: \(fixed(measuredWpm, 1)) WPM active; AutoWrite expected: \(fixed(predictedWpm, 1)) WPM     Accuracy: \(fixed(accuracy, 1))%     Corrected episodes: \(correctedMistakes)\n" + activityDescription
    }
}
struct Alignment { let end: Int, distance: Int, source: [Int] }
struct AlignedGap { var previous: Unit, current: Unit; var source = 0, event = 0; var gap = 0.0 }
struct CorrectionEpisode {
    var start: Int, end: Int, backspaces: Int, firstBackspace: Int, lastRetype: Int, firstTyped: Int
    var removed: [Unit], expected: [Unit], kind: String, notice: Double
    var repair = [Double]()
}
struct CalibrationMetrics {
    var wpm = 0.0, ordinaryGap = 0.0, activeMilliseconds = 0.0
    var ordinaryGapCount = 0
    var excludedPauses = [Double]()
    static let interruption = 10000.0
    static func measure(_ keys: [CaptureKey]) -> CalibrationMetrics {
        var result = CalibrationMetrics()
        if keys.count < 2 { return result }
        var length = 0
        for key in keys { length = key.character == 8 ? max(0, length - 1) : length + 1 }
        result.activeMilliseconds = keys.last!.milliseconds - keys[0].milliseconds
        for i in 1..<keys.count {
            let gap = keys[i].milliseconds - keys[i - 1].milliseconds
            if gap >= interruption { result.excludedPauses.append(gap); result.activeMilliseconds -= gap }
        }
        result.wpm = Double(max(0, length - 1)) * 12000 / max(1, result.activeMilliseconds)
        var live = [Int](), correction = [Bool](repeating: false, count: keys.count), retyping = 0
        for i in keys.indices {
            if keys[i].character == 8 {
                correction[i] = true; retyping += 1
                if let last = live.popLast() { correction[last] = true }
            } else {
                live.append(i)
                if retyping > 0 { correction[i] = true; retyping -= 1 }
            }
        }
        var ordinary = [Double]()
        for i in 1..<keys.count {
            let a = keys[i - 1].character, b = keys[i].character
            if !correction[i - 1] && !correction[i] && a >= 97 && a <= 122 && b >= 97 && b <= 122 && keys[i].milliseconds - keys[i - 1].milliseconds < interruption { ordinary.append(keys[i].milliseconds - keys[i - 1].milliseconds) }
        }
        result.ordinaryGap = CalibrationAnalysis.median(ordinary); result.ordinaryGapCount = ordinary.count
        return result
    }
    var activityDescription: String {
        let description = "Active typing: first to last key; gaps under 10 s included."
        if excludedPauses.isEmpty { return description + " No long pauses omitted." }
        return description + " Pauses not counted: " + excludedPauses.map { fixed($0 / 1000, 1) + " s" }.joined(separator: ", ") + "."
    }
}

enum CalibrationAnalysis {
    static let fitNames = ["UppercaseDelayPenaltyPercent", "NumberDelayPenaltyPercent", "PunctuationDelayPenaltyPercent", "ShiftSymbolDelayPenaltyPercent", "KeyPairRhythmPercent", "WordPauseMilliseconds", "SentencePauseMilliseconds", "ParagraphPauseMilliseconds"]
    static let evidenceNames = ["uppercase key gaps", "digit key gaps", "punctuation key gaps", "shifted-symbol key gaps", "key pairs", "word gaps", "sentence endings", "paragraph breaks"]
    static let left = units("qwertasdfgzxcvb12345`~!@#$%"), right = units("yuiophjklnm67890^&*()_-+={[}]|\\:;\"'<,>.?/")
    static func keyClass(_ c: Unit) -> Int {
        if TypingEngine.shifted.contains(c) { return 4 }
        if DotChar.upper(c) { return 1 }
        if DotChar.digit(c) { return 2 }
        if DotChar.punctuationSymbol(c) { return 3 }
        return 0
    }
    static func pair(_ a: Unit, _ b: Unit) -> Double {
        let a = DotChar.lower(a), b = DotChar.lower(b)
        if a == b { return 0.45 }
        let x = left.contains(a) ? 0 : right.contains(a) ? 1 : -1, y = left.contains(b) ? 0 : right.contains(b) ? 1 : -1
        return x < 0 || y < 0 ? 0 : x == y ? 0.22 : -0.15
    }
    static func align(_ actual: [Unit], _ passage: [Unit]) -> Alignment {
        let n = actual.count, m = min(passage.count, n + 12), width = m + 1
        var d = [Int32](repeating: 0, count: (n + 1) * width)
        for i in 0...n { d[i * width] = Int32(i) }
        for j in 0...m { d[j] = Int32(j) }
        if n > 0 && m > 0 {
            for i in 1...n {
                for j in 1...m {
                    let at = i * width + j
                    d[at] = min(d[at - width] + 1, min(d[at - 1] + 1, d[at - width - 1] + (actual[i - 1] == passage[j - 1] ? 0 : 1)))
                    if i > 1 && j > 1 && actual[i - 1] == passage[j - 2] && actual[i - 2] == passage[j - 1] { d[at] = min(d[at], d[at - 2 * width - 2] + 1) }
                }
            }
        }
        var end = min(n, m)
        if max(0, n - 12) <= m {
            for j in max(0, n - 12)...m {
                if d[n * width + j] < d[n * width + end] || (d[n * width + j] == d[n * width + end] && abs(j - n) < abs(end - n)) { end = j }
            }
        }
        var map = [Int](repeating: -1, count: n), x = n, y = end
        while x > 0 || y > 0 {
            let at = x * width + y
            if x > 1 && y > 1 && actual[x - 1] == passage[y - 2] && actual[x - 2] == passage[y - 1] && d[at] == d[at - 2 * width - 2] + 1 {
                map[x - 1] = y - 2; map[x - 2] = y - 1; x -= 2; y -= 2
            } else if x > 0 && y > 0 && d[at] == d[at - width - 1] + (actual[x - 1] == passage[y - 1] ? 0 : 1) {
                map[x - 1] = y - 1; x -= 1; y -= 1
            } else if x > 0 && d[at] == d[at - width] + 1 { x -= 1 }
            else { y -= 1 }
        }
        return Alignment(end: end, distance: Int(d[n * width + end]), source: map)
    }
    static func isMissedShift(_ expected: Unit, _ sent: Unit) -> Bool {
        if DotChar.upper(expected) { return DotChar.lower(expected) == sent }
        // Preserve the analysis's mapping verbatim, including its final punctuation order.
        let plain = units("`1234567890-=[]\\;'.,/")
        return TypingEngine.shifted.firstIndex(of: expected).map { plain[$0] == sent } ?? false
    }
    static func gaps(_ keys: [CaptureKey], _ passage: [Unit]) -> ([AlignedGap], [CorrectionEpisode], Alignment) {
        var corrections = [CorrectionEpisode](), buffer = [Unit](), live = [Int](), repaired = Set<Int>(), i = 0
        while i < keys.count {
            if keys[i].character != 8 { buffer.append(keys[i].character); live.append(i); i += 1; continue }
            let first = i, before = buffer, beforeAlignment = align(before, passage)
            var count = 0, firstTyped = first
            while i < keys.count && keys[i].character == 8 {
                if !buffer.isEmpty { firstTyped = live.last!; repaired.insert(live.removeLast()); buffer.removeLast(); count += 1 }
                i += 1
            }
            let afterAlignment = align(buffer, passage)
            let expected = Array(passage[afterAlignment.end..<max(afterAlignment.end, beforeAlignment.end)])
            let removed = Array(before.suffix(count))
            let kind = removed.count > expected.count ? "duplicate" : removed.count == expected.count && expected.count >= 2 && removed[0] == expected[1] && removed[1] == expected[0] && removed.dropFirst(2) == expected.dropFirst(2) ? "transpose" : !expected.isEmpty && !removed.isEmpty && isMissedShift(expected[0], removed[0]) ? "missed-shift" : "nearby"
            var episode = CorrectionEpisode(start: afterAlignment.end, end: beforeAlignment.end, backspaces: count, firstBackspace: first, lastRetype: -1, firstTyped: firstTyped, removed: removed, expected: expected, kind: kind, notice: first > 0 ? keys[first].milliseconds - keys[first - 1].milliseconds : 0)
            if first + 1 < i { for j in (first + 1)..<i { episode.repair.append(keys[j].milliseconds - keys[j - 1].milliseconds) } }
            var retype = 0
            while i < keys.count && retype < expected.count && keys[i].character == expected[retype] {
                episode.repair.append(keys[i].milliseconds - keys[i - 1].milliseconds)
                buffer.append(keys[i].character); live.append(i); repaired.insert(i); episode.lastRetype = i; retype += 1; i += 1
            }
            if count > 0 && retype == expected.count && removed != expected { corrections.append(episode) }
        }
        let final = align(buffer, passage)
        var sourceByEvent = [Int: Int]()
        for j in live.indices where final.source[j] >= 0 && buffer[j] == passage[final.source[j]] { sourceByEvent[live[j]] = final.source[j] }
        var gaps = [AlignedGap]()
        if keys.count > 1 {
            for j in 1..<keys.count {
                if !repaired.contains(j) && !repaired.contains(j - 1), let a = sourceByEvent[j - 1], let b = sourceByEvent[j], b == a + 1 {
                    gaps.append(AlignedGap(previous: passage[a], current: passage[b], source: b, event: j, gap: keys[j].milliseconds - keys[j - 1].milliseconds))
                }
            }
        }
        return (boundaryUnits(gaps).filter { !$0.contains { $0.gap >= CalibrationMetrics.interruption } }.flatMap { $0 }, corrections, final)
    }
    static func median(_ values: [Double]) -> Double {
        if values.isEmpty { return 0 }
        let values = values.sorted(), n = values.count
        return n % 2 == 1 ? values[n / 2] : (values[n / 2 - 1] + values[n / 2]) / 2
    }
    static func mean(_ values: [Double]) -> Double { var sum = 0.0; for x in values { sum += x }; return values.isEmpty ? 0 : sum / Double(values.count) }
    static func motor(_ row: AlignedGap, _ p: [Double]) -> Double {
        let c = keyClass(row.current)
        return p[0] * (c == 0 ? 1 : 1 + p[c]) * (1 + pair(row.previous, row.current) * p[5])
    }
    static func prediction(_ row: AlignedGap, _ p: [Double]) -> Double {
        var pause = TypingEngine.sentence(row.previous) ? p[7] : DotChar.whitespace(row.previous) ? p[6] : 0
        if row.previous == 10 { pause += p[8] }
        return motor(row, p) + pause
    }
    static func boundaryUnits(_ rows: [AlignedGap]) -> [[AlignedGap]] {
        var units = [[AlignedGap]](), i = 0
        while i < rows.count {
            var unit = [rows[i]]
            while DotChar.whitespace(rows[i].current) && i + 1 < rows.count && rows[i + 1].event == rows[i].event + 1 && rows[i + 1].source == rows[i].source + 1 { i += 1; unit.append(rows[i]) }
            units.append(unit); i += 1
        }
        return units
    }
    static func parameterMaximum(_ parameter: Int) -> Double {
        switch parameter { case 0: return 400; case 1...3: return 2; case 4: return 3; case 5: return 1; case 6: return 1000; default: return 3000 }
    }
    static func solve(_ input: [[Double]], _ rhs: [Double]) -> [Double] {
        var a = input, b = rhs; let n = b.count
        for i in 0..<n {
            var pivot = i
            if i + 1 < n { for j in (i + 1)..<n where abs(a[j][i]) > abs(a[pivot][i]) { pivot = j } }
            if abs(a[pivot][i]) < 1e-10 { continue }
            for k in i..<n { let t = a[i][k]; a[i][k] = a[pivot][k]; a[pivot][k] = t }
            let tb = b[i]; b[i] = b[pivot]; b[pivot] = tb
            let div = a[i][i]
            for k in i..<n { a[i][k] /= div }; b[i] /= div
            for j in 0..<n where j != i {
                let f = a[j][i]
                for k in i..<n { a[j][k] -= f * a[i][k] }; b[j] -= f * b[i]
            }
        }
        return b
    }
    static func fit(_ rows: [AlignedGap], _ current: Settings) -> [Double] {
        var p = [120.0, 0.2, 0.3, 0.3, 0.5, 0.5, Double(current["WordPauseMilliseconds"]), Double(current["SentencePauseMilliseconds"]), Double(current["ParagraphPauseMilliseconds"])]
        let units = boundaryUnits(rows)
        for _ in 0..<30 {
            var a = [[Double]](repeating: [Double](repeating: 0, count: 9), count: 9), b = [Double](repeating: 0, count: 9)
            for unit in units {
                var expected = 0.0, observed = 0.0, variance = 0.0, d = [Double](repeating: 0, count: 9)
                for r in unit {
                    let m = motor(r, p), predicted = prediction(r, p)
                    expected += predicted; observed += r.gap
                    variance += m * m * 0.12 + pow(predicted - m, 2) * 0.041 + 25
                    let c = keyClass(r.current), pair = pair(r.previous, r.current)
                    d[0] += m / p[0]
                    if c > 0 { d[c] += p[0] * (1 + pair * p[5]) }
                    d[5] += p[0] * (c == 0 ? 1 : 1 + p[c]) * pair
                    if TypingEngine.sentence(r.previous) { d[7] += 1 }
                    else if DotChar.whitespace(r.previous) { d[6] += 1 }
                    if r.previous == 10 { d[8] += 1 }
                }
                let residual = observed - expected, z = residual / sqrt(variance)
                if abs(z) > 6 { continue }
                let influence = min(1, 3 / max(3, abs(z))), weight = influence / variance
                for j in 0..<9 {
                    b[j] += weight * d[j] * residual
                    for k in 0..<9 { a[j][k] += weight * d[j] * d[k] }
                }
            }
            for j in 0..<9 { a[j][j] += 1e-7 }
            let delta = solve(a, b)
            for j in 0..<9 { p[j] = max(j == 0 ? 20 : 0, min(parameterMaximum(j), p[j] + 0.65 * delta[j])) }
        }
        return p
    }
    static let fewBelow = ["WordPauseMilliseconds":24, "SentencePauseMilliseconds":6, "ParagraphPauseMilliseconds":6, "UppercaseDelayPenaltyPercent":12, "NumberDelayPenaltyPercent":12, "PunctuationDelayPenaltyPercent":12, "ShiftSymbolDelayPenaltyPercent":12, "JitterPercent":120, "KeyPairRhythmPercent":80, "BurstPercent":40, "TypoChancePercent":6, "CorrectionNoticeMilliseconds":6, "CorrectionPauseMilliseconds":16, "CorrectionResumeMilliseconds":6, "ThinkingPauseChancePercent":8, "ThinkingPauseMilliseconds":8, "CorrectionDetectChars":16, "DuplicateChancePercent":40, "TransposeChancePercent":40, "EnableTypos":6, "NumberTypoMultiplierPercent":20, "PunctuationTypoMultiplierPercent":20, "ShiftSymbolTypoMultiplierPercent":20]
    static func describe(_ setting: String, _ count: Int, _ noun: String) -> String { "Measured from \(count) \(noun)" + (fewBelow[setting].map { count < $0 } == true ? " (few samples)." : ".") }
    static func keep(_ result: CalibrationResult, _ name: String, _ reason: String) {
        if let index = result.estimates.firstIndex(where: { $0.setting == name }) { result.estimates[index].reason = reason }
    }
    static func set(_ result: CalibrationResult, _ name: String, _ requested: Int, _ inputReason: String, _ atLimit: Bool = false) {
        let spec = Settings.spec(name), value = spec.clamp(requested)
        var reason = inputReason
        if !spec.isBool && (value != requested || atLimit) {
            let unit = name.hasSuffix("Milliseconds") ? " ms" : name.hasSuffix("Percent") ? "%" : ""
            while reason.hasSuffix(".") { reason.removeLast() }
            reason += " (limited to the \(value)\(unit)" + (requested >= spec.maximum ? " maximum)." : " minimum).")
        }
        result.proposed[name] = value
        if let i = result.estimates.firstIndex(where: { $0.setting == name }) { result.estimates[i].proposed = value; result.estimates[i].measured = true; result.estimates[i].reason = reason }
    }
    static func analyze(_ keys: [CaptureKey], seconds: Int, current: Settings) -> CalibrationResult {
        let result = CalibrationResult(current: current)
        let (gaps, allCorrections, alignment) = gaps(keys, calibrationPassage)
        result.alignedCharacters = alignment.end; result.correctedMistakes = allCorrections.count
        let metrics = CalibrationMetrics.measure(keys)
        result.measuredWpm = metrics.wpm; result.ordinaryGap = metrics.ordinaryGap; result.ordinaryGapCount = metrics.ordinaryGapCount; result.activityDescription = metrics.activityDescription
        result.accuracy = alignment.end == 0 ? 0 : max(0, 100 * (1 - Double(allCorrections.count + alignment.distance) / Double(alignment.end)))
        let quality = alignment.end == 0 ? 0 : 100 * (1 - Double(alignment.distance) / Double(alignment.end))
        let corrections = allCorrections.filter { episode in
            let end = min(keys.count - 1, episode.lastRetype + 1), start = max(1, episode.firstTyped)
            if start <= end { for i in start...end { if keys[i].milliseconds - keys[i - 1].milliseconds >= CalibrationMetrics.interruption { return false } } }
            return true
        }
        if keys.count >= 30 && quality < 70 {
            result.warning = "The typed text did not match the passage closely enough. A wrong starting point, a skipped line, or different text can cause this. All settings were kept; try again from the start."
            for i in result.estimates.indices { result.estimates[i].reason = "Kept: text matched less than 70% of the passage." }
            return result
        }
        if gaps.count < 30 { keep(result, "WordsPerMinute", "Kept: \(gaps.count) consecutive aligned gaps; need 30 for a replay target."); return result }
        let fit = fit(gaps, current); result.baseDelay = fit[0]
        var counts = [Int](repeating: 0, count: 9)
        for r in gaps {
            counts[keyClass(r.current)] += 1
            if pair(r.previous, r.current) != 0 { counts[5] += 1 }
            if r.previous == 32 || r.previous == 10 { counts[6] += 1 }
            if TypingEngine.sentence(r.previous) { counts[7] += 1 }
            if r.previous == 10 { counts[8] += 1 }
        }
        for i in 1..<9 {
            let minimum = i <= 4 ? 6 : i == 5 ? 40 : i == 6 ? 12 : 3
            if counts[i] >= minimum { set(result, fitNames[i - 1], roundEven(fit[i] * (i <= 5 ? 100 : 1)), describe(fitNames[i - 1], counts[i], evidenceNames[i - 1]), fit[i] >= parameterMaximum(i)) }
            else { keep(result, fitNames[i - 1], "Kept: n=\(counts[i]); need \(minimum) independent gaps.") }
        }
        var logs = [Double](), byEvent = [Int: Double](), orderedEvents = [Int]()
        for r in gaps {
            let m = motor(r, fit), residual = r.gap - (prediction(r, fit) - m)
            if residual > m * 0.15 && residual < m * 3 && !TypingEngine.boundary(r.previous) && !DotChar.whitespace(r.current) {
                let l = log(residual / m); logs.append(l); byEvent[r.event] = l; orderedEvents.append(r.event)
            }
        }
        if logs.count >= 60 {
            let mean = mean(logs); var variance = 0.0, covariance = 0.0, pairs = 0
            for l in logs { variance += (l - mean) * (l - mean) }; variance /= Double(logs.count)
            for event in orderedEvents { if let previous = byEvent[event - 1] { covariance += (byEvent[event]! - mean) * (previous - mean); pairs += 1 } }
            covariance = pairs > 0 ? max(0, covariance / Double(pairs)) : 0
            if pairs < 20 {
                keep(result, "BurstPercent", "Kept: \(pairs) adjacent ordinary residual pairs; need 20 to separate drift from jitter.")
                keep(result, "JitterPercent", "Kept: \(pairs) adjacent ordinary residual pairs; need 20 to separate jitter from drift.")
            } else {
                let burstVariance = min(variance, covariance / 0.935), burst = min(1, sqrt(burstVariance * 3))
                set(result, "BurstPercent", roundEven(burst * 100), describe("BurstPercent", pairs, "adjacent key pairs"))
                set(result, "JitterPercent", roundEven(sqrt(max(0, variance - burstVariance)) / 0.65 * 100), describe("JitterPercent", logs.count, "ordinary key gaps"))
            }
        } else {
            keep(result, "JitterPercent", "Kept: \(logs.count) ordinary motor residuals; need 60.")
            keep(result, "BurstPercent", "Kept: \(logs.count) ordinary motor residuals; need 60.")
        }
        var thinking = [Double](), boundaries = 0
        for unit in boundaryUnits(gaps) {
            var boundary = false, extra = 0.0, maximumMotor = 0.0
            for r in unit { boundary = boundary || TypingEngine.boundary(r.previous); extra += r.gap - prediction(r, fit); maximumMotor = max(maximumMotor, motor(r, fit)) }
            if !boundary { continue }; boundaries += 1
            if extra > max(350, maximumMotor * 2) { thinking.append(extra) }
        }
        if thinking.count >= 4 && boundaries >= 30 {
            set(result, "ThinkingPauseChancePercent", roundEven(Double(thinking.count) * 100 / Double(boundaries)), describe("ThinkingPauseChancePercent", thinking.count, "long pauses"))
            set(result, "ThinkingPauseMilliseconds", roundEven(mean(thinking) * 1.5), describe("ThinkingPauseMilliseconds", thinking.count, "long pauses"))
        } else {
            keep(result, "ThinkingPauseChancePercent", "Kept: \(thinking.count) long pauses / \(boundaries) boundaries; need 4 / 30. Transcription reads composing pauses low.")
            keep(result, "ThinkingPauseMilliseconds", "Kept: \(thinking.count) long pauses; need 4 across at least 30 boundaries.")
        }
        if corrections.count >= 3 {
            var weighted = 0.0
            for i in 0..<alignment.end {
                let c = calibrationPassage[i], cl = keyClass(c)
                if DotChar.whitespace(c) { continue }
                weighted += cl == 2 ? Double(result.proposed["NumberTypoMultiplierPercent"]) / 100 : cl == 3 ? Double(result.proposed["PunctuationTypoMultiplierPercent"]) / 100 : cl == 4 ? Double(result.proposed["ShiftSymbolTypoMultiplierPercent"]) / 100 : 1
            }
            set(result, "TypoChancePercent", roundEven(Double(corrections.count) * 100 / max(1, weighted)), describe("TypoChancePercent", corrections.count, "corrected mistakes"))
            set(result, "EnableTypos", 1, describe("EnableTypos", corrections.count, "corrected mistakes"))
            var notice = [Double](), repair = [Double](), resume = [Double]()
            for c in corrections {
                var n = c.notice
                if c.end < calibrationPassage.count && calibrationPassage[c.end] == 10 && c.end > 0 { n -= prediction(AlignedGap(previous: calibrationPassage[c.end - 1], current: 10), fit) }
                notice.append(max(0, n)); repair += c.repair
                if c.lastRetype >= 0 && c.lastRetype + 1 < keys.count && c.end > 0 && c.end < calibrationPassage.count {
                    var next = keys[c.lastRetype + 1].milliseconds - keys[c.lastRetype].milliseconds
                    if calibrationPassage[c.end] != 10 { next -= prediction(AlignedGap(previous: calibrationPassage[c.end - 1], current: calibrationPassage[c.end]), fit) }
                    resume.append(next)
                }
            }
            let repairMean = mean(repair)
            set(result, "CorrectionNoticeMilliseconds", roundEven(mean(notice)), describe("CorrectionNoticeMilliseconds", notice.count, "first Backspace presses"))
            if repair.count >= 8 { set(result, "CorrectionPauseMilliseconds", roundEven(repairMean), describe("CorrectionPauseMilliseconds", repair.count, "repair key gaps")) }
            if resume.count >= 3 { set(result, "CorrectionResumeMilliseconds", roundEven(mean(resume) - repairMean), describe("CorrectionResumeMilliseconds", resume.count, "resumes after a correction")) }
            if corrections.count >= 8 {
                var bestScore = -Double.infinity, secondScore = -Double.infinity, bestMax = current["CorrectionDetectChars"]
                for maximum in 0...8 {
                    var score = 0.0
                    for c in corrections {
                        var probability = 0.0
                        let distances = maximum == 0 ? [0] : [min(maximum, 1), min(maximum, 2), min(maximum, 3), maximum], probabilities = maximum == 0 ? [1.0] : [0.45, 0.30, 0.17, 0.08]
                        for j in distances.indices {
                            var extra = c.kind == "transpose" ? 1 : 0
                            while extra < distances[j] && c.start + extra + 1 < calibrationPassage.count && !((extra > 0 && TypingEngine.boundary(calibrationPassage[c.start + extra])) || calibrationPassage[c.start + extra + 1] == 10) { extra += 1 }
                            if extra == c.end - c.start - 1 { probability += probabilities[j] }
                        }
                        score += log(max(0.000001, probability))
                    }
                    if score > bestScore { secondScore = bestScore; bestScore = score; bestMax = maximum } else { secondScore = max(secondScore, score) }
                }
                if bestScore - secondScore >= log(3) { set(result, "CorrectionDetectChars", bestMax, describe("CorrectionDetectChars", corrections.count, "corrected mistakes")) }
                else { keep(result, "CorrectionDetectChars", "Kept: boundary-censored detection distances do not distinguish maximum distances (likelihood ratio below 3).") }
            }
            if corrections.count >= 20 {
                let dup = corrections.filter { $0.kind == "duplicate" }.count, trans = corrections.filter { $0.kind == "transpose" }.count
                set(result, "DuplicateChancePercent", roundEven(Double(dup) * 100 / Double(corrections.count)), describe("DuplicateChancePercent", corrections.count, "corrected mistakes"))
                set(result, "TransposeChancePercent", roundEven(Double(trans) * 100 / Double(corrections.count)), describe("TransposeChancePercent", corrections.count, "corrected mistakes"))
            }
        } else {
            keep(result, "EnableTypos", "Kept: fewer than 3 corrected episodes; no mistakes does not establish a zero mistake rate.")
            keep(result, "TypoChancePercent", "Kept: fewer than 3 corrected episodes; need 3.")
        }
        if corrections.count < 20 {
            keep(result, "DuplicateChancePercent", "Kept: \(corrections.count) corrected episodes; need 20 to estimate kind shares.")
            keep(result, "TransposeChancePercent", "Kept: \(corrections.count) corrected episodes; need 20 to estimate kind shares.")
        }
        var classErrors = [Int](repeating: 0, count: 5), eligible = [Int](repeating: 0, count: 5)
        for i in 0..<alignment.end where !DotChar.whitespace(calibrationPassage[i]) { eligible[keyClass(calibrationPassage[i])] += 1 }
        for c in corrections where c.start < calibrationPassage.count { classErrors[keyClass(calibrationPassage[c.start])] += 1 }
        let letterErrors = classErrors[0] + classErrors[1], letterEligible = eligible[0] + eligible[1]
        let names = ["NumberTypoMultiplierPercent", "PunctuationTypoMultiplierPercent", "ShiftSymbolTypoMultiplierPercent"]
        for cl in 2...4 {
            if letterErrors >= 10 && classErrors[cl] >= 10 && eligible[cl] >= 30 {
                set(result, names[cl - 2], roundEven(Double(classErrors[cl]) / Double(eligible[cl]) / (Double(letterErrors) / Double(letterEligible)) * 100), describe(names[cl - 2], classErrors[cl], "class mistakes"))
            } else { keep(result, names[cl - 2], "Kept: class/letter errors=\(classErrors[cl])/\(letterErrors); need at least 10 each and 30 class opportunities.") }
        }
        if letterErrors >= 10 { set(result, "TypoChancePercent", roundEven(Double(letterErrors) * 100 / Double(letterEligible)), describe("TypoChancePercent", letterErrors, "corrected mistakes")) }
        keep(result, "KeepTargetWpm", "Kept: timing-budget preference is not identifiable from a single typing recording.")
        CalibrationReplay.matchSpeed(result, seconds)
        return result
    }
}

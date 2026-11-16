import Foundation

enum CalibrationReplay {
    static func forecast(_ settings: Settings, _ seconds: Int) -> CalibrationMetrics {
        var prediction = CalibrationMetrics()
        for i in 0..<9 {
            let runtime = VirtualRuntime(seed: 70001 + i * 101, limit: seconds * 1000)
            do { try TypingEngine(runtime).type(calibrationPassage, options: TypingOptions(settings: settings)) }
            catch AutoWriteError.cancelled {} catch { preconditionFailure("Virtual replay failed: \(error)") }
            let metrics = CalibrationMetrics.measure(runtime.keys)
            prediction.wpm += metrics.wpm / 9
            prediction.ordinaryGap += metrics.ordinaryGap / 9
        }
        return prediction
    }
    static func matchSpeed(_ result: CalibrationResult, _ seconds: Int) {
        var settings = result.proposed
        let measured = result.ordinaryGap
        settings["WordsPerMinute"] = 5; let slow = forecast(settings, seconds)
        settings["WordsPerMinute"] = 180; let fast = forecast(settings, seconds)
        result.minimumWpm = slow.wpm; result.maximumWpm = fast.wpm
        var low = 5, high = 180, best = 5, bestError = abs(slow.ordinaryGap - measured), bestPrediction = slow
        while low <= high {
            let mid = (low + high) / 2; settings["WordsPerMinute"] = mid
            let predicted = forecast(settings, seconds), error = abs(predicted.ordinaryGap - measured)
            if error < bestError { bestError = error; best = mid; bestPrediction = predicted }
            if predicted.ordinaryGap > measured { low = mid + 1 } else { high = mid - 1 }
        }
        if abs(fast.ordinaryGap - measured) < bestError { best = 180; bestPrediction = fast }
        let slowTie = bestPrediction.ordinaryGap == slow.ordinaryGap, fastTie = bestPrediction.ordinaryGap == fast.ordinaryGap
        if slowTie || fastTie {
            low = 5; high = 180
            while low <= high {
                let mid = (low + high) / 2; settings["WordsPerMinute"] = mid
                let predicted = forecast(settings, seconds), tied = predicted.ordinaryGap == bestPrediction.ordinaryGap
                if tied { best = mid; bestPrediction = predicted }
                if slowTie ? tied : !tied { low = mid + 1 } else { high = mid - 1 }
            }
        }
        CalibrationAnalysis.set(result, "WordsPerMinute", best, "Measured from \(result.ordinaryGapCount) ordinary letter gaps: \(fixed(measured, 1)) ms; replay: \(fixed(bestPrediction.ordinaryGap, 1)) ms. Target chosen by forward replay.")
        result.predictedWpm = bestPrediction.wpm; result.predictedOrdinaryGap = bestPrediction.ordinaryGap
        if measured > slow.ordinaryGap * 1.05 || measured < fast.ordinaryGap * 0.95 {
            result.warning = "The 45 to 220 ms motor limit prevents matching your ordinary letter rhythm. Measured \(fixed(result.measuredWpm, 1)) WPM; AutoWrite will type approximately \(fixed(result.predictedWpm, 1)) WPM with the closest achievable Target WPM (\(best))."
            if let i = result.estimates.firstIndex(where: { $0.setting == "WordsPerMinute" }) { result.estimates[i].closestAchievable = true; result.estimates[i].reason = "Closest achievable: " + result.estimates[i].reason }
        } else if abs(result.predictedWpm - result.measuredWpm) > result.measuredWpm * 0.05 {
            result.warning = result.predictedWpm < result.measuredWpm
                ? "AutoWrite adds thinking pauses and mistakes at the proposed settings, so it types slower overall. To get closer to your test speed, set Thinking pause % and Mistake rate % to 0 and run the test again."
                : "Your recording had pauses AutoWrite does not reproduce at the proposed settings, so AutoWrite types faster overall."
        }
    }
}

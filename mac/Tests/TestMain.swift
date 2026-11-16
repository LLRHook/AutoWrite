import Foundation

@main struct Tests {
    static func main() {
        setbuf(stdout, nil)
        let runner = TestRunner()
        var counts = ""
        do {
            let directory = try dataDirectory()
            guard ProcessInfo.processInfo.environment["AUTOWRITE_DATA_DIR"]?.hasPrefix("/") == true else { fatalError("tests require an isolated data directory") }
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
            let fixtures = Fixtures(root: URL(fileURLWithPath: CommandLine.arguments[1]))
            runner.test("reference tracked files are all readable") { try fixtures.verifyTrackedFiles() }
            try runner.core(fixtures, directory)
            try runner.calibration(fixtures, directory)
            let evidence = URL(fileURLWithPath: CommandLine.arguments[2], isDirectory: true)
            try FileManager.default.createDirectory(at: evidence, withIntermediateDirectories: true)
            try runner.windows(fixtures, directory, evidence)
            runner.system(fixtures, evidence)
            try runner.review(directory, evidence)
            counts = fixtures.countSummary
        } catch { runner.test("test setup") { throw error } }
        print("TOTAL: \(runner.passed) PASS, \(runner.failed) FAIL; reference cases compared: \(runner.referenceCases); skipped: \(runner.skipped); \(counts)")
        exit(runner.failed == 0 && runner.passed > 0 ? 0 : 1)
    }
}

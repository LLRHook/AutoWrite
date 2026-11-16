import Foundation

@main struct Tests {
    static func main() {
        setbuf(stdout, nil)
        let runner = TestRunner()
        do {
            let directory = try dataDirectory()
            guard ProcessInfo.processInfo.environment["AUTOWRITE_DATA_DIR"]?.hasPrefix("/") == true else { fatalError("tests require an isolated data directory") }
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
            let fixtures = Fixtures(root: URL(fileURLWithPath: CommandLine.arguments[1]))
            try runner.core(fixtures, directory)
            try runner.calibration(fixtures, directory)
            let evidence = URL(fileURLWithPath: CommandLine.arguments[2], isDirectory: true)
            try FileManager.default.createDirectory(at: evidence, withIntermediateDirectories: true)
            try runner.windows(fixtures, directory, evidence)
            runner.system(fixtures, evidence)
        } catch { runner.test("test setup") { throw error } }
        print("TOTAL: \(runner.passed) PASS, \(runner.failed) FAIL; reference cases compared: \(runner.referenceCases)")
        exit(runner.failed == 0 ? 0 : 1)
    }
}

import XCTest
@testable import Polishly

/// Diagnostic harness for the "Revise with AI" custom-instruction path. Not a
/// pass/fail regression suite — it drives real provider calls and dumps
/// results to disk for manual quality review. Loads the already-saved
/// Keychain key directly (allowInteraction: false) so it never triggers a
/// prompt in an unattended run.
final class ReviseQualityTests: XCTestCase {

    struct Case {
        let label: String
        let text: String
        let instruction: String
    }

    static let shortTexts: [String] = [
        "Hey can you send me the report when you get a chance? Thanks!",
        "I think we should push the meeting to next week.",
        "Sorry I missed your call, I was in a meeting all day."
    ]

    static let mediumTexts: [String] = [
        "Our Q3 numbers came in below target, mostly because of the delayed product launch. The engineering team hit a few last-minute bugs that pushed the release by three weeks. Marketing had already spent a chunk of the budget on the original launch date, so some of that spend is now sunk. We're revising the Q4 forecast to account for the slower start. I'd like to get the team together this week to talk through next steps.",
        "The new onboarding flow requires users to verify their email, set up two-factor authentication, and complete a short profile before they can access the dashboard. Early data shows about 40% of users drop off during the 2FA step. We think the setup process is too long relative to the value users see up front. Simplifying this could meaningfully improve activation.",
        "I wanted to follow up on the proposal we sent last week. We haven't heard back and wanted to check if you had any questions or needed additional information. We're happy to jump on a call if that would be easier. Let us know what works for your schedule.",
        "The server has been intermittently returning 502 errors during peak traffic hours, roughly between 2pm and 4pm daily. Our monitoring shows CPU usage spiking above 90% during these windows. We suspect the caching layer isn't handling the load correctly. We're planning to add a second cache node by Friday."
    ]

    static let longTexts: [String] = [
        """
        Team,

        Wanted to give everyone a detailed status update on the migration project as we head into the final stretch before launch.

        On the infrastructure side, we've finished provisioning the new database cluster and completed the first full data sync from the legacy system. The sync took about six hours, which is within the window we planned for. We ran a checksum comparison afterward and found a small number of mismatched rows, all of which turned out to be timezone conversion issues in the old system rather than problems with the new one.

        The application team has finished porting about 85% of the API endpoints to the new service. The remaining 15% are mostly reporting endpoints that depend on the analytics warehouse, and we're blocked there until the data team finishes their schema migration, expected by Thursday.

        QA found four bugs during this week's regression pass. Two were cosmetic issues in the admin dashboard, one was a genuine data corruption bug in the billing export (now fixed and verified), and one is still open — an intermittent race condition in the session handler that only reproduces under load.

        On the timeline, we are still targeting a soft launch for internal users on the 28th, with the public rollout the following week assuming no major issues surface during the internal period.

        Please flag anything I've missed and let me know if you need more support to hit your parts of this timeline.

        Thanks,
        Alex
        """,
        """
        Project Retro: Q2 Website Redesign

        What went well:
        The design team delivered final mockups two days ahead of schedule, which gave engineering extra buffer before the sprint deadline. Cross-functional communication was noticeably better this quarter — the weekly sync between design, engineering, and marketing caught several scope mismatches early, before they became rework. The new component library cut implementation time for repeated UI patterns by roughly a third compared to the last redesign.

        What didn't go well:
        We underestimated the complexity of migrating the existing blog content into the new CMS structure. This ended up consuming almost two extra weeks that weren't in the original plan. Several stakeholders were looped in late, which led to a round of last-minute feedback that forced us to redo the homepage hero section three times. QA testing on older browsers was also rushed at the end because it wasn't scheduled as its own workstream.

        Action items for next quarter:
        We should scope content migration work explicitly and separately from visual design work going forward, since they have very different risk profiles. Stakeholder review checkpoints need to happen earlier and be treated as blocking gates rather than optional check-ins. Browser compatibility testing should be built into the sprint plan from day one instead of being squeezed in at the end.

        Overall the redesign shipped successfully and initial engagement metrics look positive, but the process had enough friction that we want to tighten it before the next major project.
        """,
        """
        Dear Ms. Rodriguez,

        I'm writing to give you an update on the custom furniture order you placed with us on March 3rd, order number 48291.

        Unfortunately, we've run into a delay with the walnut veneer that was originally sourced for your dining table. Our supplier informed us this week that the batch we had reserved did not pass their quality inspection, and the next available batch of matching grain won't arrive until early next month. We know this pushes your delivery date back significantly from what we originally promised, and we're sorry for the inconvenience this causes, especially given that you mentioned needing the table before a family gathering.

        We looked into a few alternatives on our end. We could substitute a different but very similar walnut veneer that's currently in stock, which would let us keep your original delivery date. Alternatively, we can wait for the original batch, which keeps the exact grain match you selected in the showroom, but adds roughly five weeks to the timeline. We're also happy to offer a partial refund on the order as a gesture given the delay, regardless of which option you choose.

        Please let us know which option you'd prefer, or if you'd like to discuss further over the phone. We have your number on file and are happy to call at a time that's convenient for you.

        We value your business and want to make this right.

        Sincerely,
        The Craftwood Furniture Team
        """
    ]

    static let cases: [Case] = [
        Case(label: "short-1-formal", text: shortTexts[0], instruction: "make this more formal"),
        Case(label: "short-2-spanish", text: shortTexts[1], instruction: "translate this to Spanish"),
        Case(label: "short-3-apologetic", text: shortTexts[2], instruction: "make it sound apologetic"),
        Case(label: "medium-1-concise-two-sentences", text: mediumTexts[0], instruction: "make this concise — cut it to two sentences"),
        Case(label: "medium-2-bulleted-list", text: mediumTexts[1], instruction: "rewrite this as a bulleted list"),
        Case(label: "medium-3-confident-cta", text: mediumTexts[2], instruction: "make the tone more confident and add a clear call to action"),
        Case(label: "medium-4-professional-closing", text: mediumTexts[3], instruction: "add a professional closing line"),
        Case(label: "long-1-simplify-nontechnical", text: longTexts[0], instruction: "simplify this for a non-technical reader"),
        Case(label: "long-2-casual-friendly", text: longTexts[1], instruction: "make it more casual and friendly"),
        Case(label: "long-3-numbered-steps", text: longTexts[2], instruction: "convert this into numbered steps")
    ]

    static let outputPath = "/private/tmp/claude-504/-Users-ktatheka-Projects-polishly/231d1836-1925-43a2-8afe-1b06364ea847/scratchpad/revise_results.txt"

    func testReviseWithAICustomInstructions() async throws {
        let appState = AppState.shared
        let originalProvider = appState.selectedProvider
        
        if originalProvider == .demo {
            appState.selectedProvider = .cerebras
        }
        let provider = appState.selectedProvider
        XCTAssertNotEqual(provider, .demo, "selectedProvider is .demo — no real provider configured; cannot exercise Revise with AI end to end.")

        if appState.apiKey.isEmpty {
            let service = "com.polishly.apiKey.\(provider.rawValue)"
            let result = KeychainHelper.shared.read(service: service, account: "user", allowInteraction: false)
            if case .success(let data) = result, let key = String(data: data, encoding: .utf8), !key.isEmpty {
                appState.apiKey = key
            }
        }
        XCTAssertFalse(appState.apiKey.isEmpty, "No API key available in memory or Keychain for provider \(provider.rawValue).")

        var report = "PROVIDER=\(provider.rawValue) MODEL=\(appState.modelName)\n\n"

        for (index, c) in Self.cases.enumerated() {
            if index > 0 {
                try await Task.sleep(for: .seconds(20))
            }

            var result = ""
            var errorText: String? = nil
            for attempt in 0..<3 {
                result = ""
                errorText = nil
                do {
                    try await RewriteClient.shared.rewriteStream(
                        text: c.text,
                        tone: "custom",
                        customInstruction: c.instruction,
                        context: nil
                    ) { current in
                        result = current
                    }
                    break
                } catch {
                    errorText = "\(error)"
                    let isRateLimit = errorText?.lowercased().contains("tokens per minute") == true
                        || errorText?.lowercased().contains("rate limit") == true
                    if isRateLimit && attempt < 2 {
                        try await Task.sleep(for: .seconds(30))
                        continue
                    }
                    break
                }
            }

            report += "===== \(c.label) =====\n"
            report += "INSTRUCTION: \(c.instruction)\n"
            report += "INPUT_CHARS: \(c.text.count)  INPUT_LINES: \(c.text.split(separator: "\n").count)\n"
            if let errorText {
                report += "ERROR: \(errorText)\n"
            } else {
                report += "OUTPUT_CHARS: \(result.count)\n"
                report += "OUTPUT:\n\(result)\n"
            }
            report += "\n"
        }

        try? report.write(toFile: Self.outputPath, atomically: true, encoding: .utf8)
        print("Wrote revise quality report to \(Self.outputPath)")
        
        appState.selectedProvider = originalProvider
    }

    /// Stress-tests the "no explicit max_tokens" hypothesis: asks for a large
    /// expansion of an already-long input so the model wants to produce a
    /// long completion, then checks whether the stream got cut off mid-sentence.
    func testLongOutputTruncationStress() async throws {
        let appState = AppState.shared
        let originalProvider = appState.selectedProvider
        
        if originalProvider == .demo {
            appState.selectedProvider = .cerebras
        }
        let provider = appState.selectedProvider
        if appState.apiKey.isEmpty {
            let service = "com.polishly.apiKey.\(provider.rawValue)"
            let result = KeychainHelper.shared.read(service: service, account: "user", allowInteraction: false)
            if case .success(let data) = result, let key = String(data: data, encoding: .utf8), !key.isEmpty {
                appState.apiKey = key
            }
        }
        XCTAssertFalse(appState.apiKey.isEmpty)

        let stressText = (Self.longTexts[0] + "\n\n" + Self.longTexts[1] + "\n\n" + Self.longTexts[2])
        let instruction = "Expand this significantly with much more detail and thorough explanation for every point — be as long and thorough as possible."

        var result = ""
        try await RewriteClient.shared.rewriteStream(
            text: stressText,
            tone: "custom",
            customInstruction: instruction,
            context: nil
        ) { current in
            result = current
        }

        let trimmed = result.trimmingCharacters(in: .whitespacesAndNewlines)
        let lastChar = trimmed.last
        let endsCleanly = lastChar.map { ".!?\"'".contains($0) } ?? false

        let report = """
        PROVIDER=\(provider.rawValue) MODEL=\(appState.modelName)
        INPUT_CHARS=\(stressText.count)
        OUTPUT_CHARS=\(result.count)
        ENDS_CLEANLY=\(endsCleanly)
        LAST_200_CHARS: \(trimmed.suffix(200))

        FULL_OUTPUT:
        \(result)
        """
        try report.write(
            toFile: "/private/tmp/claude-504/-Users-ktatheka-Projects-polishly/231d1836-1925-43a2-8afe-1b06364ea847/scratchpad/truncation_stress.txt",
            atomically: true,
            encoding: .utf8
        )
        print("Stress test: input=\(stressText.count) chars, output=\(result.count) chars, endsCleanly=\(endsCleanly)")
        appState.selectedProvider = originalProvider
    }
}

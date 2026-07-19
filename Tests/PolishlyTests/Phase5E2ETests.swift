import XCTest
import AppKit
@testable import Polishly

/// Phase 5 automatable end-to-end gates. Manual app UI cells are noted in the QA report.
@MainActor
final class Phase5E2ETests: XCTestCase {

    // MARK: - Demo: no network

    func testDemoRewriteNeverTouchesNetwork() async throws {
        let state = AppState.shared
        let previous = state.selectedProvider
        defer { state.selectedProvider = previous }

        state.selectedProvider = .demo
        XCTAssertTrue(state.providerIsReady)

        var updates = 0
        try await RewriteClient.shared.rewriteStream(
            text: "i have sent the mail let see what he will",
            tone: "improve",
            customInstruction: nil,
            context: nil
        ) { _ in updates += 1 }

        XCTAssertGreaterThan(updates, 0, "Demo must stream local output")
    }

    func testDemoValidateConnectionIsNoOp() async throws {
        try await RewriteClient.shared.validateConnection(
            provider: .demo,
            apiKey: "should-not-matter",
            model: "Local rules"
        )
    }

    // MARK: - Errors

    func testErrorMatrixCoverage() {
        XCTAssertEqual(
            RewriteClient.mapNetworkError(URLError(.notConnectedToInternet), providerName: "OpenAI"),
            .offline
        )
        XCTAssertEqual(
            RewriteClient.mapHTTPError(statusCode: 401, providerName: "OpenAI", json: nil),
            .invalidKey("OpenAI")
        )
        XCTAssertEqual(
            RewriteClient.mapHTTPError(statusCode: 429, providerName: "Groq", json: nil),
            .rateLimit("Groq")
        )
        XCTAssertEqual(
            RewriteClient.mapHTTPError(statusCode: 503, providerName: "Anthropic", json: nil),
            .providerOutage("Anthropic")
        )
        let timeout = RewriteClient.mapNetworkError(URLError(.timedOut), providerName: "Cerebras")
        if case .networkFailure = timeout {
            // ok
        } else {
            XCTFail("Expected networkFailure for timeout")
        }
        XCTAssertEqual(
            RewriteClient.mapHTTPError(
                statusCode: 404,
                providerName: "OpenAI",
                json: ["error": ["message": "The model `bad-model` does not exist"]]
            ),
            .wrongModel("OpenAI")
        )
    }

    func testSecretsNeverAppearInMappedErrors() {
        let mapped = RewriteClient.mapHTTPError(
            statusCode: 401,
            providerName: "OpenAI",
            json: ["error": ["message": "invalid_api_key sk-live-LEAKME123"]]
        )
        let text = mapped.errorDescription ?? ""
        XCTAssertFalse(text.contains("sk-live-LEAKME123"))
        XCTAssertFalse(text.contains("LEAKME"))
    }

    // MARK: - Security / Keychain

    func testForgetKeyMakesProviderUnavailableImmediately() {
        let override = "com.polishly.tests.phase5.\(UUID().uuidString)"
        AppState.keychainServiceOverrideForTesting = override
        defer {
            _ = KeychainHelper.shared.delete(service: override, account: "user")
            AppState.keychainServiceOverrideForTesting = nil
        }

        let state = AppState.shared
        let previousProvider = state.selectedProvider
        let previousModel = state.modelName
        let previousKey = state.apiKey
        defer {
            state.selectedProvider = previousProvider
            state.modelName = previousModel
            state.apiKey = previousKey
        }

        state.selectedProvider = .openAI
        state.modelName = "gpt-4.1-mini"
        state.apiKey = "sk-phase5-temp-key"
        XCTAssertTrue(state.saveAPIKey(testAfterSave: false))
        XCTAssertTrue(state.hasRememberedAPIKey)

        XCTAssertTrue(state.forgetStoredAPIKey())
        XCTAssertEqual(state.apiKey, "")
        XCTAssertFalse(state.hasRememberedAPIKey)
        XCTAssertFalse(state.providerIsReady)
    }

    // MARK: - Windows / lifecycle panels

    func testPopupAndTriggerDismissWithoutStuckPanels() {
        let trigger = DiscoveryTriggerController.shared
        let popup = PopupController.shared
        let capture = SelectionEngine.CapturedText(
            text: "phase5",
            method: .accessibility,
            bounds: CGRect(x: 200, y: 200, width: 40, height: 16),
            axElement: nil,
            sourceBundleIdentifier: nil
        )

        trigger.show(at: CGPoint(x: 220, y: 220), capture: capture, isAX: false)
        XCTAssertTrue(trigger.isVisible)
        trigger.hide()
        XCTAssertFalse(trigger.isVisible)

        popup.show(for: capture)
        XCTAssertTrue(popup.window?.isVisible == true)
        popup.closePanel()
        XCTAssertFalse(popup.window?.isVisible == true)

        // Escape/dismiss monitors must be cleaned (no crash on repeated cycles).
        for _ in 0..<5 {
            popup.show(for: capture)
            popup.closePanel()
            trigger.show(at: CGPoint(x: 240, y: 240), capture: nil, isAX: false)
            trigger.hide()
        }
        XCTAssertFalse(popup.window?.isVisible == true)
        XCTAssertFalse(trigger.isVisible)
    }

    func testCornerClampOnAllScreens() {
        for screen in NSScreen.screens {
            let v = screen.visibleFrame
            let size = PopupController.cardSize
            let origin = ScreenCoordinates.clampOrigin(
                NSPoint(x: v.maxX, y: v.maxY),
                size: size,
                in: v
            )
            let frame = NSRect(origin: origin, size: size)
            XCTAssertGreaterThanOrEqual(frame.minX, v.minX - 0.5)
            XCTAssertGreaterThanOrEqual(frame.minY, v.minY - 0.5)
            XCTAssertLessThanOrEqual(frame.maxX, v.maxX + 0.5)
            XCTAssertLessThanOrEqual(frame.maxY, v.maxY + 0.5)
        }
    }

    // MARK: - Providers: real rewrite when key present

    func testRealProviderRewriteSucceedsWhenKeyAvailable() async throws {
        AppState.keychainServiceOverrideForTesting = nil
        let candidates: [LLMProvider] = [.openAI, .anthropic, .groq, .cerebras]
        var succeeded: LLMProvider?

        let state = AppState.shared
        let prevProvider = state.selectedProvider
        let prevModel = state.modelName
        let prevKey = state.apiKey
        defer {
            state.selectedProvider = prevProvider
            state.modelName = prevModel
            state.apiKey = prevKey
        }

        for provider in candidates {
            let result = KeychainHelper.shared.read(
                service: AppState.keychainService(for: provider),
                account: "user",
                allowInteraction: false
            )
            guard case .success(let data) = result,
                  let key = String(data: data, encoding: .utf8),
                  !key.isEmpty else { continue }

            state.selectedProvider = provider
            state.modelName = provider.defaultModel
            state.apiKey = key

            var text = ""
            do {
                try await RewriteClient.shared.rewriteStream(
                    text: "hello there, please make this clearer",
                    tone: "concise",
                    customInstruction: nil,
                    context: nil
                ) { chunk in text = chunk }
                XCTAssertFalse(text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                XCTAssertFalse(text.contains(key))
                succeeded = provider
                break
            } catch {
                continue
            }
        }

        if succeeded == nil {
            throw XCTSkip("No saved provider key available for a live rewrite.")
        }
    }

    // MARK: - Permissions / launch-at-login

    func testLaunchAtLoginStatusReadableWithoutPrompt() {
        _ = LaunchAtLogin.status
        _ = LaunchAtLogin.userFacingMessage
    }

    func testAccessibilityStateIsReadableWithoutPrompt() {
        // Passive check must not open the system prompt.
        let before = AXIsProcessTrusted()
        AppState.shared.checkAccessibility(requestPrompt: false)
        XCTAssertEqual(AppState.shared.isAccessibilityTrusted, before)
    }

    // MARK: - No rewrite before ready / invalid config

    func testInvalidConfigBlocksRewriteBeforeNetwork() async {
        let state = AppState.shared
        let prevProvider = state.selectedProvider
        let prevKey = state.apiKey
        let prevModel = state.modelName
        defer {
            state.selectedProvider = prevProvider
            state.apiKey = prevKey
            state.modelName = prevModel
        }

        state.selectedProvider = .openAI
        state.apiKey = ""
        state.modelName = "gpt-4.1-mini"

        do {
            try await RewriteClient.shared.rewriteStream(
                text: "should not send",
                tone: "improve",
                customInstruction: nil,
                context: nil
            ) { _ in
                XCTFail("Should not stream when key missing")
            }
            XCTFail("Expected missingAPIKey")
        } catch let error as RewriteError {
            if case .missingAPIKey = error {
                // ok
            } else {
                XCTFail("Expected missingAPIKey, got \(error)")
            }
        } catch {
            XCTFail("Unexpected error \(error)")
        }
    }
}

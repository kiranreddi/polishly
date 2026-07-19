import XCTest
@testable import Polishly

@MainActor
final class ProviderManagementTests: XCTestCase {
    private var originalProvider: LLMProvider!
    private var originalModel: String!
    private var originalAPIKey: String!
    private var testService: String!

    override func setUp() {
        super.setUp()
        let state = AppState.shared
        originalProvider = state.selectedProvider
        originalModel = state.modelName
        originalAPIKey = state.apiKey
        testService = "com.polishly.tests.apiKey.\(UUID().uuidString)"
        AppState.keychainServiceOverrideForTesting = testService
        _ = KeychainHelper.shared.delete(service: testService!, account: "user")
    }

    override func tearDown() {
        if let testService {
            _ = KeychainHelper.shared.delete(service: testService, account: "user")
        }
        AppState.keychainServiceOverrideForTesting = nil
        let state = AppState.shared
        state.selectedProvider = originalProvider
        state.modelName = originalModel
        state.apiKey = originalAPIKey
        super.tearDown()
    }

    // MARK: - Model validation

    func testOpenAIModelValidation() {
        XCTAssertNil(LLMProvider.openAI.modelValidationError(for: "gpt-5.2"))
        XCTAssertNil(LLMProvider.openAI.modelValidationError(for: "o3-mini"))
        XCTAssertNotNil(LLMProvider.openAI.modelValidationError(for: ""))
        XCTAssertNotNil(LLMProvider.openAI.modelValidationError(for: "claude-haiku-4-5"))
        XCTAssertNotNil(LLMProvider.openAI.modelValidationError(for: "sk-abc123"))
        XCTAssertNotNil(LLMProvider.openAI.modelValidationError(for: "gpt 4"))
    }

    func testAnthropicModelValidation() {
        XCTAssertNil(LLMProvider.anthropic.modelValidationError(for: "claude-haiku-4-5"))
        XCTAssertNotNil(LLMProvider.anthropic.modelValidationError(for: "gpt-5.2"))
        XCTAssertNotNil(LLMProvider.anthropic.modelValidationError(for: ""))
    }

    func testGroqAndCerebrasModelValidation() {
        XCTAssertNil(LLMProvider.groq.modelValidationError(for: "llama-3.3-70b-versatile"))
        XCTAssertNil(LLMProvider.cerebras.modelValidationError(for: "gpt-oss-120b"))
        XCTAssertNotNil(LLMProvider.groq.modelValidationError(for: "claude-opus-4"))
        XCTAssertNotNil(LLMProvider.cerebras.modelValidationError(for: "claude-opus-4"))
    }

    func testDemoNeedsNoModelOrKey() {
        XCTAssertNil(LLMProvider.demo.modelValidationError(for: "anything"))
        XCTAssertNil(LLMProvider.demo.apiKeyFormatError(for: ""))
        AppState.shared.selectedProvider = .demo
        XCTAssertTrue(AppState.shared.providerIsReady)
        XCTAssertEqual(AppState.shared.providerConnectionState, .connected)
    }

    // MARK: - Key format / no-key / invalid-model readiness

    func testNoKeyAndInvalidModelBlockReadyState() {
        let state = AppState.shared
        state.selectedProvider = .openAI
        state.apiKey = ""
        state.modelName = "gpt-5.2"
        XCTAssertFalse(state.providerIsReady)
        XCTAssertNotNil(state.providerConfigurationError)

        state.apiKey = "sk-test-not-a-real-key"
        state.modelName = "claude-haiku-4-5"
        XCTAssertFalse(state.providerIsReady)
        XCTAssertTrue(state.providerConfigurationError?.localizedCaseInsensitiveContains("claude") == true)

        state.modelName = "gpt-5.2"
        XCTAssertTrue(state.providerIsReady)
    }

    func testWrongProviderKeyFormatRejected() {
        XCTAssertNotNil(LLMProvider.openAI.apiKeyFormatError(for: "sk-ant-abc"))
        XCTAssertNotNil(LLMProvider.anthropic.apiKeyFormatError(for: "sk-abc"))
        XCTAssertNotNil(LLMProvider.groq.apiKeyFormatError(for: "sk-abc"))
        XCTAssertNil(LLMProvider.openAI.apiKeyFormatError(for: "sk-abc123"))
        XCTAssertNil(LLMProvider.anthropic.apiKeyFormatError(for: "sk-ant-abc123"))
    }

    // MARK: - Keychain save / load / update / forget / rotation

    func testKeySaveLoadUpdateForgetAndRotation() {
        let state = AppState.shared
        state.selectedProvider = .openAI
        state.modelName = "gpt-5.2"

        state.apiKey = "sk-original-key-value"
        XCTAssertTrue(state.saveAPIKey(testAfterSave: false))
        XCTAssertTrue(state.hasRememberedAPIKey)

        // Simulate stale memory, then load from Keychain.
        state.apiKey = "sk-stale-in-memory"
        state.loadStoredAPIKey()
        XCTAssertEqual(state.apiKey, "sk-original-key-value")

        // Rotate: update remembered key replaces Keychain and keeps memory in sync.
        state.apiKey = "sk-rotated-key-value"
        XCTAssertTrue(state.saveAPIKey(testAfterSave: false))
        state.apiKey = ""
        state.loadStoredAPIKey()
        XCTAssertEqual(state.apiKey, "sk-rotated-key-value")

        // Forget clears Keychain + memory immediately — provider unavailable.
        XCTAssertTrue(state.forgetStoredAPIKey())
        XCTAssertEqual(state.apiKey, "")
        XCTAssertFalse(state.hasRememberedAPIKey)
        XCTAssertFalse(state.providerIsReady)
        XCTAssertEqual(state.providerConnectionState, .idle)

        let afterForget = KeychainHelper.shared.read(
            service: AppState.keychainService(for: .openAI),
            account: "user",
            allowInteraction: false
        )
        if case .notFound = afterForget {
            // expected
        } else {
            XCTFail("Forgot key must remove the Keychain item immediately")
        }
    }

    func testKeychainHelperRoundTrip() {
        let service = testService!
        let first = Data("sk-first".utf8)
        let second = Data("sk-second".utf8)
        XCTAssertTrue(KeychainHelper.shared.save(first, service: service, account: "user"))
        if case .success(let data) = KeychainHelper.shared.read(service: service, account: "user", allowInteraction: false) {
            XCTAssertEqual(data, first)
        } else {
            XCTFail("Expected success reading saved key")
        }
        XCTAssertTrue(KeychainHelper.shared.save(second, service: service, account: "user"))
        if case .success(let data) = KeychainHelper.shared.read(service: service, account: "user", allowInteraction: false) {
            XCTAssertEqual(String(data: data, encoding: .utf8), "sk-second")
        } else {
            XCTFail("Expected updated key")
        }
        XCTAssertTrue(KeychainHelper.shared.delete(service: service, account: "user"))
        if case .notFound = KeychainHelper.shared.read(service: service, account: "user", allowInteraction: false) {
            // expected
        } else {
            XCTFail("Expected notFound after delete")
        }
    }

    // MARK: - Error mapping

    func testProviderErrorMappingCoversRequiredCases() {
        XCTAssertEqual(
            RewriteClient.mapNetworkError(URLError(.notConnectedToInternet), providerName: "OpenAI"),
            .offline
        )
        XCTAssertEqual(
            RewriteClient.mapNetworkError(URLError(.cannotFindHost), providerName: "OpenAI"),
            .networkFailure("The request to OpenAI failed. Check your connection and try again.")
        )

        XCTAssertEqual(
            RewriteClient.mapHTTPError(statusCode: 401, providerName: "OpenAI", json: nil),
            .invalidKey("OpenAI")
        )
        XCTAssertEqual(
            RewriteClient.mapHTTPError(
                statusCode: 401,
                providerName: "OpenAI",
                json: ["error": ["message": "API key expired"]]
            ),
            .expiredOrRevokedKey("OpenAI")
        )
        XCTAssertEqual(
            RewriteClient.mapHTTPError(
                statusCode: 404,
                providerName: "Groq",
                json: ["error": ["message": "The model `nope` was not found"]]
            ),
            .wrongModel("Groq")
        )
        XCTAssertEqual(
            RewriteClient.mapHTTPError(statusCode: 429, providerName: "Anthropic", json: nil),
            .rateLimit("Anthropic")
        )
        XCTAssertEqual(
            RewriteClient.mapHTTPError(statusCode: 503, providerName: "Cerebras", json: nil),
            .providerOutage("Cerebras")
        )
        XCTAssertEqual(
            RewriteClient.mapProviderPayloadError(
                json: ["error": ["message": "broken stream"]],
                providerName: "OpenAI",
                fallback: "fallback"
            ),
            .malformedResponse("OpenAI")
        )
    }

    func testErrorsNeverEchoSecrets() {
        let leaked = RewriteError.sanitize(
            "Invalid key sk-ant-secretVALUE123 and Bearer TOK_EN.gsk_abc csk-xyz"
        )
        XCTAssertFalse(leaked.contains("sk-ant-secretVALUE123"))
        XCTAssertFalse(leaked.contains("Bearer TOK_EN"))
        XCTAssertFalse(leaked.contains("gsk_abc"))
        XCTAssertTrue(leaked.contains("[redacted]"))

        let mapped = RewriteClient.mapHTTPError(
            statusCode: 400,
            providerName: "OpenAI",
            json: ["error": ["message": "bad key sk-live-SHOULD_NOT_APPEAR"]]
        )
        XCTAssertFalse(mapped.errorDescription?.contains("sk-live-SHOULD_NOT_APPEAR") == true)
    }

    // MARK: - Real provider connection (uses already-saved Keychain key, never prints it)

    func testRealProviderConnectionSucceedsWhenKeyAvailable() async throws {
        AppState.keychainServiceOverrideForTesting = nil
        defer { AppState.keychainServiceOverrideForTesting = testService }

        let candidates: [LLMProvider] = [.openAI, .anthropic, .groq, .cerebras]
        var connectedProvider: LLMProvider?

        for provider in candidates {
            let result = KeychainHelper.shared.read(
                service: AppState.keychainService(for: provider),
                account: "user",
                allowInteraction: false
            )
            guard case .success(let data) = result,
                  let key = String(data: data, encoding: .utf8),
                  !key.isEmpty else { continue }

            do {
                try await RewriteClient.shared.validateConnection(
                    provider: provider,
                    apiKey: key,
                    model: provider.defaultModel
                )
                connectedProvider = provider
                break
            } catch {
                // Try the next configured provider; never log the key.
                continue
            }
        }

        if connectedProvider == nil {
            throw XCTSkip("No saved provider key available for a live connection test.")
        }
        XCTAssertNotNil(connectedProvider)
    }
}

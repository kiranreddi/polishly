import XCTest
@testable import Polishly

@MainActor
final class OnboardingTests: XCTestCase {
    
    private var initialProvider: LLMProvider = .demo
    private var initialProviderString: String?
    
    override func setUp() {
        super.setUp()
        initialProvider = AppState.shared.selectedProvider
        initialProviderString = UserDefaults.standard.string(forKey: "selectedProvider")
        UserDefaults.standard.removeObject(forKey: "onboardingCompleted")
        UserDefaults.standard.removeObject(forKey: "selectedProvider")
    }
    
    override func tearDown() {
        AppState.shared.onboardingState = .ready
        AppState.shared.selectedProvider = initialProvider
        AppState.shared.showOnboarding = false
        UserDefaults.standard.removeObject(forKey: "onboardingCompleted")
        if let str = initialProviderString {
            UserDefaults.standard.set(str, forKey: "selectedProvider")
        } else {
            UserDefaults.standard.removeObject(forKey: "selectedProvider")
        }
        super.tearDown()
    }
    
    func testOnboardingStateLogic() {
        let state = AppState.shared
        
        state.onboardingState = .ready
        XCTAssertTrue(UserDefaults.standard.bool(forKey: "onboardingCompleted"))
        XCTAssertFalse(state.showOnboarding)
        
        state.onboardingState = .providerMissing
        XCTAssertTrue(state.showOnboarding)
        
        state.showOnboarding = false
        XCTAssertEqual(state.onboardingState, .ready)
        XCTAssertTrue(UserDefaults.standard.bool(forKey: "onboardingCompleted"))
    }
    
    func testOnboardingDemoModeBypass() {
        let state = AppState.shared

        state.onboardingState = .providerMissing
        state.selectedProvider = .demo

        state.onboardingState = .ready

        XCTAssertEqual(state.onboardingState, .ready)
        XCTAssertTrue(UserDefaults.standard.bool(forKey: "onboardingCompleted"))
        XCTAssertFalse(state.showOnboarding)
    }

    // MARK: - First-run state machine (pure, no real AX/network dependency)

    func testFirstLaunchWithNoAccessibilityAccess() {
        XCTAssertEqual(AppState.initialOnboardingState(trusted: false, onboardingCompleted: false), .accessibilityMissing)
        XCTAssertEqual(AppState.initialOnboardingState(trusted: false, onboardingCompleted: true), .accessibilityMissing)
    }

    func testFirstLaunchWithAccessibilityButNoOnboarding() {
        XCTAssertEqual(AppState.initialOnboardingState(trusted: true, onboardingCompleted: false), .providerMissing)
    }

    func testRestartAfterSetupCompletionStaysReady() {
        XCTAssertEqual(AppState.initialOnboardingState(trusted: true, onboardingCompleted: true), .ready)
    }

    func testReturningFromSystemSettingsGrantsAccess() {
        XCTAssertEqual(
            AppState.onboardingStateAfterAccessibilityChange(current: .accessibilityMissing, trusted: true),
            .accessibilityGranted
        )
        // No system prompt loop: an already-granted state doesn't re-trigger.
        XCTAssertNil(AppState.onboardingStateAfterAccessibilityChange(current: .accessibilityGranted, trusted: true))
    }

    func testAccessibilityRevokedMidSessionDoesNotReopenOnboarding() {
        // Onboarding is a one-time setup flow, not a live permission display —
        // Settings already shows current accessibility status. Losing trust
        // after the user is fully onboarded must not silently flip state with
        // no visible window to show for it.
        XCTAssertNil(AppState.onboardingStateAfterAccessibilityChange(current: .ready, trusted: false))
    }

    // MARK: - Provider setup reachable and revertible during onboarding

    func testProviderConnectedRevertsWhenConfigurationChanges() {
        let state = AppState.shared
        state.onboardingState = .providerMissing

        // Simulate what testProviderConnection's success path sets.
        state.onboardingState = .providerConnected
        XCTAssertEqual(state.onboardingState, .providerConnected)

        // Editing the key after a successful test invalidates that test —
        // "Continue" must not stay available for an unverified change.
        state.apiKey = "sk-changed-after-connecting"
        XCTAssertEqual(state.onboardingState, .providerMissing)
    }

    /// "First launch with a saved provider key" — exercises the real
    /// onboarding provider-setup path end to end using an already-saved
    /// Keychain key, the same live-connection pattern as
    /// ProviderManagementTests.testRealProviderConnectionSucceedsWhenKeyAvailable.
    func testFirstLaunchWithSavedProviderKeyReachesProviderConnected() async throws {
        let state = AppState.shared
        AppState.keychainServiceOverrideForTesting = nil

        let candidates: [LLMProvider] = [.openAI, .anthropic, .groq, .cerebras]
        var configured: (provider: LLMProvider, key: String)?
        for provider in candidates {
            let result = KeychainHelper.shared.read(
                service: AppState.keychainService(for: provider),
                account: "user",
                allowInteraction: false
            )
            if case .success(let data) = result, let key = String(data: data, encoding: .utf8), !key.isEmpty {
                configured = (provider, key)
                break
            }
        }
        guard let configured else {
            throw XCTSkip("No saved provider key available to exercise the onboarding provider-connected path.")
        }

        state.onboardingState = .providerMissing
        state.selectedProvider = configured.provider
        state.apiKey = configured.key
        state.testProviderConnection()

        let deadline = Date().addingTimeInterval(20)
        while state.isTestingProviderConnection && Date() < deadline {
            try await Task.sleep(for: .milliseconds(100))
        }

        XCTAssertEqual(state.providerConnectionState, .connected)
        XCTAssertEqual(state.onboardingState, .providerConnected)
    }
}

import XCTest
@testable import Polishly

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
}

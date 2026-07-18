import XCTest
@testable import Polishly

final class AppLifecycleTests: XCTestCase {
    
    func testLaunchDecision_WhenOnboardingIncomplete_ShouldShowOnboarding() {
        let decision = AppDelegate.launchDecision(showOnboarding: true)
        XCTAssertEqual(decision, .showOnboarding)
    }
    
    func testLaunchDecision_WhenOnboardingComplete_ShouldStartSilently() {
        let decision = AppDelegate.launchDecision(showOnboarding: false)
        XCTAssertEqual(decision, .startSilently)
    }
}

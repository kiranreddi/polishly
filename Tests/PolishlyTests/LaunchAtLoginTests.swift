import XCTest
@testable import Polishly

final class LaunchAtLoginTests: XCTestCase {

    func testStatusAPIIsCallableWithoutCrashing() {
        let status = LaunchAtLogin.status
        XCTAssertTrue(
            [LaunchAtLogin.Status.enabled,
             .disabled,
             .requiresApproval,
             .notFound,
             .unknown].contains(status)
        )
        XCTAssertFalse(LaunchAtLogin.userFacingMessage.isEmpty)
    }

    func testDisableWhenAlreadyOffIsClean() throws {
        // Clean disable: unregister while already off must not throw.
        guard LaunchAtLogin.status == .disabled else {
            // Don't flip a developer's enabled preference during unit tests.
            return
        }
        _ = try LaunchAtLogin.setEnabled(false)
        XCTAssertEqual(LaunchAtLogin.status, .disabled)
    }
}

import XCTest
@testable import Polishly

final class RepeatedInteractionTests: XCTestCase {

    @MainActor
    func testTriggerShowHideCycles() {
        let controller = DiscoveryTriggerController.shared

        for i in 0..<10 {
            controller.show(at: CGPoint(x: 100 + i, y: 100), capture: nil, isAX: false)
            XCTAssertTrue(controller.isVisible)
            XCTAssertEqual(controller.test_window?.frame.size, DiscoveryTriggerController.triggerSize)

            controller.hide()
            XCTAssertFalse(controller.isVisible)
        }
    }

    @MainActor
    func testPopupShowHideCycles() {
        let controller = PopupController.shared
        let capture = SelectionEngine.CapturedText(
            text: "Cycle",
            method: .accessibility,
            bounds: CGRect(x: 120, y: 120, width: 10, height: 10),
            axElement: nil,
            sourceBundleIdentifier: nil
        )

        for _ in 0..<10 {
            controller.show(for: capture)
            XCTAssertTrue(controller.window?.isVisible == true)
            XCTAssertEqual(controller.window?.frame.size, PopupController.cardSize)

            controller.closePanel()
            XCTAssertFalse(controller.window?.isVisible == true)
        }
    }

    @MainActor
    func testNoCrashStressLoopAlternatingTriggerAndPopup() {
        let trigger = DiscoveryTriggerController.shared
        let popup = PopupController.shared
        let capture = SelectionEngine.CapturedText(
            text: "Stress",
            method: .accessibility,
            bounds: CGRect(x: 300, y: 300, width: 40, height: 18),
            axElement: nil,
            sourceBundleIdentifier: nil
        )

        for i in 0..<50 {
            if i % 2 == 0 {
                trigger.show(at: CGPoint(x: 150 + i, y: 200 + (i % 7)), capture: capture, isAX: false)
                XCTAssertTrue(trigger.isVisible)
                trigger.hide()
                XCTAssertFalse(trigger.isVisible)
            } else {
                popup.show(for: capture)
                XCTAssertTrue(popup.window?.isVisible == true)
                popup.closePanel()
                XCTAssertFalse(popup.window?.isVisible == true)
            }
        }

        // Singletons must not accumulate extra windows.
        XCTAssertLessThanOrEqual(NSApp.windows.filter { $0.title.isEmpty || $0 is NSPanel }.count, 8)
    }

    @MainActor
    func testRepeatedShowDoesNotCreateDuplicatePopupWindows() {
        let popup = PopupController.shared
        let before = ObjectIdentifier(popup.window as AnyObject)
        let capture = SelectionEngine.CapturedText(
            text: "Once",
            method: .accessibility,
            bounds: CGRect(x: 50, y: 50, width: 20, height: 12),
            axElement: nil,
            sourceBundleIdentifier: nil
        )

        for _ in 0..<20 {
            popup.show(for: capture)
        }
        defer { popup.closePanel() }

        XCTAssertEqual(ObjectIdentifier(popup.window as AnyObject), before)
        XCTAssertEqual(popup.window?.frame.size, PopupController.cardSize)
    }
}

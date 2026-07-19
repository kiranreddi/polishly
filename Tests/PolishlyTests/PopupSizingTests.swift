import XCTest
import SwiftUI
@testable import Polishly

final class PopupSizingTests: XCTestCase {

    @MainActor
    func testPopupControllerHostingViewSizingOptionsDisabled() {
        let controller = PopupController.shared
        let capture = SelectionEngine.CapturedText(
            text: "Test",
            method: .accessibility,
            bounds: CGRect(x: 100, y: 100, width: 50, height: 20),
            axElement: nil,
            sourceBundleIdentifier: nil
        )

        controller.show(for: capture)
        defer { controller.closePanel() }

        guard let window = controller.window else {
            XCTFail("Popup window should be instantiated")
            return
        }

        guard let hostingView = window.contentView as? NSHostingView<PopupCardView> else {
            XCTFail("Popup window contentView should be an NSHostingView<PopupCardView>")
            return
        }

        XCTAssertEqual(hostingView.sizingOptions, [])
        XCTAssertEqual(window.frame.width, PopupController.cardSize.width)
        XCTAssertEqual(window.frame.height, PopupController.cardSize.height)
    }

    @MainActor
    func testPopupSizeStableAcrossRepeatedShows() {
        let controller = PopupController.shared
        let capture = SelectionEngine.CapturedText(
            text: "Stable size",
            method: .accessibility,
            bounds: CGRect(x: 200, y: 200, width: 40, height: 18),
            axElement: nil,
            sourceBundleIdentifier: nil
        )

        for _ in 0..<5 {
            controller.show(for: capture)
            XCTAssertEqual(controller.window?.frame.size, PopupController.cardSize)
            controller.closePanel()
        }
    }

    @MainActor
    func testTriggerFixedSizeRegression() {
        let controller = DiscoveryTriggerController.shared
        controller.show(at: CGPoint(x: 120, y: 120), capture: nil, isAX: false)
        defer { controller.hide() }

        guard let window = controller.test_window else {
            XCTFail("Trigger window should exist")
            return
        }
        XCTAssertEqual(window.frame.size, DiscoveryTriggerController.triggerSize)

        guard let hostingView = window.contentView as? NSHostingView<DiscoveryTriggerView> else {
            XCTFail("Trigger content should be NSHostingView<DiscoveryTriggerView>")
            return
        }
        XCTAssertEqual(hostingView.sizingOptions, [])
    }
}

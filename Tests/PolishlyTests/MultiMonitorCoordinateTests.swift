import XCTest
import AppKit
@testable import Polishly

final class MultiMonitorCoordinateTests: XCTestCase {

    func testAccessibilityToAppKitConversionFlipsAroundMainDisplayHeight() {
        let mainHeight = CGDisplayBounds(CGMainDisplayID()).height
        let cgRect = CGRect(x: 100, y: 50, width: 80, height: 24)
        let appKit = ScreenCoordinates.appKitRect(fromAccessibility: cgRect)

        XCTAssertEqual(appKit.origin.x, 100)
        XCTAssertEqual(appKit.width, 80)
        XCTAssertEqual(appKit.height, 24)
        XCTAssertEqual(appKit.origin.y, mainHeight - cgRect.maxY, accuracy: 0.001)
        XCTAssertEqual(appKit.maxY, mainHeight - cgRect.minY, accuracy: 0.001)
    }

    func testClampKeepsWindowInsideVisibleFrameEdges() {
        let visible = NSRect(x: 0, y: 0, width: 1000, height: 800)
        let size = NSSize(width: 430, height: 300)

        // Far past top-right
        let topRight = ScreenCoordinates.clampOrigin(NSPoint(x: 5000, y: 5000), size: size, in: visible)
        XCTAssertEqual(topRight.x, visible.maxX - size.width - 12, accuracy: 0.001)
        XCTAssertEqual(topRight.y, visible.maxY - size.height - 12, accuracy: 0.001)

        // Far past bottom-left
        let bottomLeft = ScreenCoordinates.clampOrigin(NSPoint(x: -400, y: -400), size: size, in: visible)
        XCTAssertEqual(bottomLeft.x, visible.minX + 12, accuracy: 0.001)
        XCTAssertEqual(bottomLeft.y, visible.minY + 12, accuracy: 0.001)
    }

    @MainActor
    func testPopupClampedToScreenContainingSelection() {
        guard let screen = NSScreen.main ?? NSScreen.screens.first else {
            XCTFail("No screen available")
            return
        }

        let visible = screen.visibleFrame
        let mainHeight = CGDisplayBounds(CGMainDisplayID()).height

        // Selection near the top-right corner of the visible frame (in AppKit),
        // converted back to Accessibility/CG space for show().
        let appKitSelection = NSRect(
            x: visible.maxX - 40,
            y: visible.maxY - 40,
            width: 30,
            height: 20
        )
        let cgBounds = CGRect(
            x: appKitSelection.origin.x,
            y: mainHeight - appKitSelection.maxY,
            width: appKitSelection.width,
            height: appKitSelection.height
        )

        let controller = PopupController.shared
        let capture = SelectionEngine.CapturedText(
            text: "Corner",
            method: .accessibility,
            bounds: cgBounds,
            axElement: nil,
            sourceBundleIdentifier: nil
        )
        controller.show(for: capture)
        defer { controller.closePanel() }

        guard let frame = controller.window?.frame else {
            XCTFail("Missing popup frame")
            return
        }

        XCTAssertGreaterThanOrEqual(frame.minX, visible.minX - 0.5)
        XCTAssertGreaterThanOrEqual(frame.minY, visible.minY - 0.5)
        XCTAssertLessThanOrEqual(frame.maxX, visible.maxX + 0.5)
        XCTAssertLessThanOrEqual(frame.maxY, visible.maxY + 0.5)
        XCTAssertEqual(frame.size, PopupController.cardSize)
    }

    @MainActor
    func testTriggerClampedOnEveryScreenCorner() {
        let size = DiscoveryTriggerController.triggerSize
        let controller = DiscoveryTriggerController.shared

        for screen in NSScreen.screens {
            let visible = screen.visibleFrame
            let corners = [
                NSPoint(x: visible.minX + 1, y: visible.maxY - 1),
                NSPoint(x: visible.maxX - 1, y: visible.maxY - 1),
                NSPoint(x: visible.minX + 1, y: visible.minY + size.height + 1),
                NSPoint(x: visible.maxX - 1, y: visible.minY + size.height + 1)
            ]

            for point in corners {
                controller.show(at: point, capture: nil, isAX: false)
                guard let frame = controller.test_window?.frame else {
                    XCTFail("Trigger window missing")
                    controller.hide()
                    return
                }
                XCTAssertTrue(
                    frameIsInsideSomeVisibleDisplay(frame),
                    "Trigger frame \(frame) left all visible display frames for point \(point)"
                )
                controller.hide()
            }
        }
    }

    private func frameIsInsideSomeVisibleDisplay(_ frame: NSRect) -> Bool {
        NSScreen.screens.contains { screen in
            let v = screen.visibleFrame
            return frame.minX >= v.minX - 0.5
                && frame.minY >= v.minY - 0.5
                && frame.maxX <= v.maxX + 0.5
                && frame.maxY <= v.maxY + 0.5
        }
    }

    func testRetinaBackingScaleDoesNotAffectLogicalClamp() {
        // Clamp math is in points; backing scale must not change logical origin.
        let visible = NSRect(x: 0, y: 0, width: 1440, height: 900)
        let size = PopupController.cardSize
        let origin = ScreenCoordinates.clampOrigin(NSPoint(x: 1400, y: 880), size: size, in: visible)
        XCTAssertEqual(origin.x, 1440 - size.width - 12, accuracy: 0.001)
        XCTAssertEqual(origin.y, 900 - size.height - 12, accuracy: 0.001)
    }
}

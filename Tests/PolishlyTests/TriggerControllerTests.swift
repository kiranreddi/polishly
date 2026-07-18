import XCTest
import SwiftUI
@testable import Polishly

final class TriggerControllerTests: XCTestCase {
    
    @MainActor
    func testTriggerControllerHostingViewSizingOptionsDisabled() {
        let controller = DiscoveryTriggerController.shared
        
        // Show trigger at arbitrary point to instantiate the window
        controller.show(at: .zero, capture: nil, isAX: false)
        
        guard let window = controller.test_window else {
            XCTFail("Trigger window should be instantiated")
            return
        }
        
        
        // Ensure we don't leak the window
        defer { controller.test_window?.close() }
        
        guard let hostingView = window.contentView as? NSHostingView<DiscoveryTriggerView> else {
            XCTFail("Trigger window contentView should be an NSHostingView<DiscoveryTriggerView>")
            return
        }
        
        // Assert sizing options are empty to prevent NSGenericException layout loop
        XCTAssertEqual(hostingView.sizingOptions, [])
    }
}

import XCTest
@testable import Polishly

@MainActor
final class PopupViewModelTests: XCTestCase {
    
    func testInitialState() {
        let viewModel = PopupViewModel()
        XCTAssertFalse(viewModel.isTierC)
        XCTAssertFalse(viewModel.isPasteSentUnconfirmable)
        XCTAssertFalse(viewModel.isError)
        XCTAssertFalse(viewModel.isStreaming)
        XCTAssertEqual(viewModel.selectedTab, "improve")
    }
    
    func testConfiguration() {
        let viewModel = PopupViewModel()
        let capture = SelectionEngine.CapturedText(
            text: "Hello",
            method: .accessibility,
            bounds: nil,
            axElement: nil,
            sourceBundleIdentifier: "com.apple.Notes"
        )
        
        viewModel.isTierC = true // simulate previous state
        viewModel.isPasteSentUnconfirmable = true
        viewModel.configure(with: capture)
        XCTAssertFalse(viewModel.hasContext)
        XCTAssertFalse(viewModel.isTierC)
        XCTAssertFalse(viewModel.isPasteSentUnconfirmable)
        XCTAssertEqual(viewModel.contextMessage, "No thread context available — using only your selected text")
    }
}

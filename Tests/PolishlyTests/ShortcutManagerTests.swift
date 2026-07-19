import XCTest
@testable import Polishly
import Carbon.HIToolbox

@MainActor
final class ShortcutManagerTests: XCTestCase {
    
    var originalKeyCode: Int!
    var originalModifiers: Int!
    
    override func setUp() {
        super.setUp()
        originalKeyCode = UserDefaults.standard.integer(forKey: "shortcutKeyCode")
        originalModifiers = UserDefaults.standard.integer(forKey: "shortcutModifiers")
    }
    
    override func tearDown() {
        UserDefaults.standard.set(originalKeyCode, forKey: "shortcutKeyCode")
        UserDefaults.standard.set(originalModifiers, forKey: "shortcutModifiers")
        super.tearDown()
    }
    
    func testPersistenceAndReload() {
        let appState = AppState.shared
        
        appState.shortcutKeyCode = 123
        appState.shortcutModifiers = 4096 // controlKey only
        
        XCTAssertEqual(UserDefaults.standard.integer(forKey: "shortcutKeyCode"), 123)
        XCTAssertEqual(UserDefaults.standard.integer(forKey: "shortcutModifiers"), 4096)
    }
}

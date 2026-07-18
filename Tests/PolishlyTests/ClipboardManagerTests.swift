import XCTest
@testable import Polishly
import Cocoa

final class ClipboardManagerTests: XCTestCase {
    
    var originalSnapshot: ClipboardManager.Snapshot!
    var originalChangeCount: Int!
    
    override func setUp() {
        super.setUp()
        originalSnapshot = ClipboardManager.shared.takeSnapshot()
        originalChangeCount = NSPasteboard.general.changeCount
    }
    
    override func tearDown() {
        _ = ClipboardManager.shared.restore(snapshot: originalSnapshot, expectedChangeCount: NSPasteboard.general.changeCount)
        super.tearDown()
    }
    
    func testSnapshotAndRestore() {
        let manager = ClipboardManager.shared
        let pb = NSPasteboard.general
        
        pb.clearContents()
        pb.setString("Original Test String", forType: .string)
        let initialCount = pb.changeCount
        
        let snapshot = manager.takeSnapshot()
        
        XCTAssertEqual(snapshot.changeCount, initialCount)
        XCTAssertEqual(snapshot.items.count, pb.pasteboardItems?.count ?? 0)
        
        let newCount = manager.writeString("Injected Test String")
        
        XCTAssertNotEqual(newCount, initialCount)
        XCTAssertEqual(pb.string(forType: .string), "Injected Test String")
        
        // Restore with correct expected change count
        let restored = manager.restore(snapshot: snapshot, expectedChangeCount: newCount)
        XCTAssertTrue(restored)
        XCTAssertEqual(pb.string(forType: .string), "Original Test String")
    }
    
    func testRestoreFailsIfClipboardChangedExternally() {
        let manager = ClipboardManager.shared
        let pb = NSPasteboard.general
        
        pb.clearContents()
        pb.setString("Original Test String", forType: .string)
        
        let snapshot = manager.takeSnapshot()
        
        let newCount = manager.writeString("Injected Test String")
        
        // Simulate external copy
        pb.clearContents()
        pb.setString("External User Copy", forType: .string)
        let externalCount = pb.changeCount
        
        // Restore should fail because the change count no longer matches newCount
        let restored = manager.restore(snapshot: snapshot, expectedChangeCount: newCount)
        XCTAssertFalse(restored)
        XCTAssertEqual(pb.string(forType: .string), "External User Copy") // User's copy is preserved
    }
}

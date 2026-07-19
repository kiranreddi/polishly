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
        XCTAssertEqual(externalCount, pb.changeCount)
    }

    func testWaitForPasteboardChangeDetectsWrite() {
        let manager = ClipboardManager.shared
        let pb = NSPasteboard.general
        let before = pb.changeCount

        DispatchQueue.global(qos: .userInitiated).asyncAfter(deadline: .now() + 0.05) {
            _ = manager.writeString("polishly-wait-change-\(UUID().uuidString)")
        }

        XCTAssertTrue(manager.waitForPasteboardChange(from: before, timeout: 0.5))
        XCTAssertNotEqual(pb.changeCount, before)
    }

    func testWaitForPasteboardChangeTimesOut() {
        let manager = ClipboardManager.shared
        let before = NSPasteboard.general.changeCount
        XCTAssertFalse(manager.waitForPasteboardChange(from: before, timeout: 0.06))
        XCTAssertEqual(NSPasteboard.general.changeCount, before)
    }
}

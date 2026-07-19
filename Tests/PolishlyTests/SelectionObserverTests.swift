import XCTest
import Combine
@testable import Polishly

@MainActor
final class SelectionObserverTests: XCTestCase {
    
    func testIdempotentStart() {
        let observer = SelectionObserver.shared
        
        // Ensure starting clean
        observer.stop()
        XCTAssertFalse(observer.test_isStarted)
        XCTAssertFalse(observer.test_hasEventMonitor)
        
        observer.start()
        
        XCTAssertTrue(observer.test_isStarted)
        XCTAssertTrue(observer.test_hasEventMonitor)
        
        // Start again
        observer.start()
        
        XCTAssertTrue(observer.test_isStarted)
        XCTAssertTrue(observer.test_hasEventMonitor)
        
        observer.stop()
        
        XCTAssertFalse(observer.test_isStarted)
        XCTAssertFalse(observer.test_hasEventMonitor)
        XCTAssertFalse(observer.test_hasAXObserver)
        XCTAssertNil(observer.test_currentlyObservedFocusedElement)
    }
    
    func testAXObserverCurrentlyObservedStateClearedOnStop() {
        let observer = SelectionObserver.shared
        // Although we cannot easily inject a fake AXUIElement and AXObserver without UI tests,
        // we can verify that the internal tracked state is nil initially and after stop().
        observer.stop()
        XCTAssertNil(observer.test_currentlyObservedFocusedElement)
    }
    
    func testCalculateDistance() {
        let observer = SelectionObserver.shared
        let p1 = NSPoint(x: 0, y: 0)
        let p2 = NSPoint(x: 3, y: 4)
        let distance = observer.calculateDistance(from: p1, to: p2)
        XCTAssertEqual(distance, 5.0)
    }
    
    func testMouseDownLocationClearedOnStop() {
        let observer = SelectionObserver.shared
        observer.test_mouseDownLocation = NSPoint(x: 100, y: 100)
        XCTAssertNotNil(observer.test_mouseDownLocation)
        
        observer.start()
        observer.test_mouseDownLocation = NSPoint(x: 100, y: 100)
        observer.stop()
        
        XCTAssertNil(observer.test_mouseDownLocation)
    }
    
    func testPollingLifecycleAndDeduplication() {
        let observer = SelectionObserver.shared
        observer.stop()
        
        XCTAssertFalse(observer.test_pollingTimerActive)
        XCTAssertNil(observer.test_lastSelectionSignature)
        
        observer.start()
        XCTAssertTrue(observer.test_pollingTimerActive)
        
        // Synthesize a signature and ensure it clears on stop
        observer.test_lastSelectionSignature = SelectionObserver.SelectionSignature(text: "test", bounds: nil, pid: 123)
        XCTAssertNotNil(observer.test_lastSelectionSignature)
        
        observer.stop()
        XCTAssertFalse(observer.test_pollingTimerActive)
        XCTAssertNil(observer.test_lastSelectionSignature)
    }
}

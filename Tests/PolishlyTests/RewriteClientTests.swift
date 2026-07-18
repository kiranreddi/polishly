import XCTest
@testable import Polishly

final class RewriteClientTests: XCTestCase {
    
    // Test the specific error messages directly since they are tightly coupled to the validate function
    func testErrorMapping() async {
        // Test Offline mapping
        let offlineError = URLError(.notConnectedToInternet)
        let offlineMapped = RewriteClient.mapNetworkError(offlineError, providerName: "TestProvider")
        if case .apiError(let msg) = offlineMapped {
            XCTAssertTrue(msg.contains("offline"))
        } else {
            XCTFail("Expected .apiError for offline mapping")
        }
        
        // Test Timeout mapping
        let timeoutError = URLError(.timedOut)
        let timeoutMapped = RewriteClient.mapNetworkError(timeoutError, providerName: "TestProvider")
        if case .apiError(let msg) = timeoutMapped {
            XCTAssertTrue(msg.contains("timed out"))
        } else {
            XCTFail("Expected .apiError for timeout mapping")
        }

        // Test HTTP 401 mapping
        let authMapped = RewriteClient.mapHTTPError(statusCode: 401, providerName: "TestProvider", json: nil)
        if case .apiError(let msg) = authMapped {
            XCTAssertTrue(msg.contains("rejected this API key"))
        } else {
            XCTFail("Expected .apiError for 401 mapping")
        }

        // Test HTTP 429 mapping
        let rateLimitMapped = RewriteClient.mapHTTPError(statusCode: 429, providerName: "TestProvider", json: nil)
        if case .apiError(let msg) = rateLimitMapped {
            XCTAssertTrue(msg.contains("rate limit reached"))
        } else {
            XCTFail("Expected .apiError for 429 mapping")
        }

        // Test HTTP 5xx mapping
        let serverMapped = RewriteClient.mapHTTPError(statusCode: 503, providerName: "TestProvider", json: nil)
        if case .apiError(let msg) = serverMapped {
            XCTAssertTrue(msg.contains("experiencing server issues"))
        } else {
            XCTFail("Expected .apiError for 5xx mapping")
        }
    }
}

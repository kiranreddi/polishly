import XCTest
@testable import Polishly

final class RewriteClientTests: XCTestCase {

    func testErrorMapping() async {
        let offlineMapped = RewriteClient.mapNetworkError(
            URLError(.notConnectedToInternet),
            providerName: "TestProvider"
        )
        XCTAssertEqual(offlineMapped, .offline)

        let timeoutMapped = RewriteClient.mapNetworkError(
            URLError(.timedOut),
            providerName: "TestProvider"
        )
        if case .networkFailure(let msg) = timeoutMapped {
            XCTAssertTrue(msg.contains("TestProvider"))
        } else {
            XCTFail("Expected .networkFailure for timeout mapping")
        }

        XCTAssertEqual(
            RewriteClient.mapHTTPError(statusCode: 401, providerName: "TestProvider", json: nil),
            .invalidKey("TestProvider")
        )
        XCTAssertEqual(
            RewriteClient.mapHTTPError(statusCode: 429, providerName: "TestProvider", json: nil),
            .rateLimit("TestProvider")
        )
        XCTAssertEqual(
            RewriteClient.mapHTTPError(statusCode: 503, providerName: "TestProvider", json: nil),
            .providerOutage("TestProvider")
        )
    }
}

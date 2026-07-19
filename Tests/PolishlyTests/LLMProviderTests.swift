import XCTest
@testable import Polishly

final class LLMProviderTests: XCTestCase {
    
    func testProviderDefaultsArePubliclyCompatible() {
        XCTAssertEqual(LLMProvider.openAI.defaultModel, "gpt-5.2", "OpenAI default should be a public API model, not an internal Codex string.")
        XCTAssertEqual(LLMProvider.cerebras.defaultModel, "gpt-oss-120b", "Cerebras default should remain unchanged as tested live.")
        XCTAssertEqual(LLMProvider.groq.defaultModel, "llama-3.3-70b-versatile", "Groq default must be a valid public Groq string.")
        XCTAssertEqual(LLMProvider.anthropic.defaultModel, "claude-haiku-4-5", "Anthropic default must be a valid public model string.")

        XCTAssertNil(LLMProvider.openAI.modelValidationError(for: LLMProvider.openAI.defaultModel))
        XCTAssertNil(LLMProvider.groq.modelValidationError(for: LLMProvider.groq.defaultModel))
        XCTAssertNil(LLMProvider.cerebras.modelValidationError(for: LLMProvider.cerebras.defaultModel))
        XCTAssertNil(LLMProvider.anthropic.modelValidationError(for: LLMProvider.anthropic.defaultModel))
    }
}

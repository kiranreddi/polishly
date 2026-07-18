import Foundation

enum AnthropicError: Error {
    case invalidURL
    case missingAPIKey
    case networkError(Error)
    case parseError
    case apiError(String)
}

class AnthropicClient {
    static let shared = AnthropicClient()
    private let endpoint = URL(string: "https://api.anthropic.com/v1/messages")!
    
    func rewriteStream(text: String, tone: String, customInstruction: String?, context: String?, onUpdate: @escaping (String) -> Void) async throws {
        let apiKey = AppState.shared.rewriteAPIKey
        guard !apiKey.isEmpty else {
            let demo = Self.demoRewrite(text: text, tone: tone, instruction: customInstruction)
            var streamed = ""
            for word in demo.split(separator: " ") {
                try await Task.sleep(for: .milliseconds(22))
                streamed += (streamed.isEmpty ? "" : " ") + word
                onUpdate(streamed)
            }
            return
        }
        
        var request = URLRequest(url: endpoint)
        request.httpMethod = "POST"
        request.addValue(apiKey, forHTTPHeaderField: "x-api-key")
        request.addValue("2023-06-01", forHTTPHeaderField: "anthropic-version")
        request.addValue("application/json", forHTTPHeaderField: "Content-Type")
        
        var prompt = "Rewrite the following text."
        if let context = context {
            prompt += " Here is the context of the conversation:\n<context>\n\(context)\n</context>\n"
        }
        
        if tone == "custom", let instruction = customInstruction {
            prompt += " Instruction: \(instruction)\n"
        } else {
            switch tone {
            case "improve": prompt += " Make it sound more professional and clear.\n"
            case "concise": prompt += " Make it concise and to the point.\n"
            case "friendly": prompt += " Make it friendly and approachable.\n"
            case "expand": prompt += " Expand on it with more detail.\n"
            default: break
            }
        }
        prompt += "\nReturn ONLY the rewritten text, with no markdown formatting, no conversational filler, and no `<text>` tags.\n\nText to rewrite: \(text)"
        
        let body: [String: Any] = [
            "model": "claude-3-haiku-20240307",
            "max_tokens": 1024,
            "stream": true,
            "messages": [
                ["role": "user", "content": prompt]
            ]
        ]
        
        request.httpBody = try JSONSerialization.data(withJSONObject: body)
        
        let (result, response) = try await URLSession.shared.bytes(for: request)
        
        guard let httpResponse = response as? HTTPURLResponse, httpResponse.statusCode == 200 else {
            throw AnthropicError.apiError("Status code: \((response as? HTTPURLResponse)?.statusCode ?? 0)")
        }
        
        var currentText = ""
        
        for try await line in result.lines {
            if line.hasPrefix("data: ") {
                let jsonStr = String(line.dropFirst(6))
                if jsonStr == "[DONE]" { break }
                if let data = jsonStr.data(using: .utf8),
                   let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
                   let type = json["type"] as? String {
                    if type == "content_block_delta",
                       let delta = json["delta"] as? [String: Any],
                       let textDelta = delta["text"] as? String {
                        currentText += textDelta
                        onUpdate(currentText)
                    }
                }
            }
        }
    }

    private static func demoRewrite(text: String, tone: String, instruction: String?) -> String {
        let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return trimmed }
        var polished = trimmed
            .replacingOccurrences(of: " let see ", with: " let's see ", options: .caseInsensitive)
            .replacingOccurrences(of: " im ", with: " I'm ", options: .caseInsensitive)
        polished = polished.prefix(1).uppercased() + polished.dropFirst()
        if let last = polished.last, !".!?".contains(last) {
            polished += "."
        }

        switch tone {
        case "concise":
            return polished.replacingOccurrences(of: "I have ", with: "I ")
        case "friendly":
            return "Just a quick note: \(polished)"
        case "expand":
            return "\(polished) Please let me know if you would like any additional detail."
        case "custom" where !(instruction ?? "").isEmpty:
            return polished
        default:
            return polished
        }
    }
}

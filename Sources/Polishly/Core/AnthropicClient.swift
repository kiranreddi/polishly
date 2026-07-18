import Foundation

enum RewriteError: LocalizedError {
    case missingAPIKey(String)
    case invalidConfiguration(String)
    case apiError(String)
    case emptyResponse

    var errorDescription: String? {
        switch self {
        case .missingAPIKey(let provider):
            return "No \(provider) API key is active. Add one in Settings or choose On-device demo."
        case .invalidConfiguration(let message), .apiError(let message):
            return message
        case .emptyResponse:
            return "The provider returned no rewritten text. Try again or choose another model."
        }
    }
}

/// Routes rewrite requests without ever reading Keychain implicitly. The only key
/// available here is the in-memory value the user entered or explicitly loaded.
final class RewriteClient {
    static let shared = RewriteClient()

    private init() {}

    /// Exercises the same provider, model, authentication, and streaming path as
    /// a real rewrite without reading or transmitting any user-selected text.
    func validateConnection(provider: LLMProvider, apiKey: String, model: String) async throws {
        guard provider != .demo else { return }
        var receivedText = false
        let prompt = Self.rewritePrompt(
            text: "Polishly connection test.",
            tone: "concise",
            customInstruction: nil,
            context: nil
        )
        try await performProviderRewrite(
            provider: provider,
            apiKey: apiKey,
            model: model,
            prompt: prompt
        ) { text in
            receivedText = receivedText || !text.isEmpty
        }
        guard receivedText else { throw RewriteError.emptyResponse }
    }

    func rewriteStream(
        text: String,
        tone: String,
        customInstruction: String?,
        context: String?,
        onUpdate: @escaping (String) -> Void
    ) async throws {
        let appState = AppState.shared
        let provider = appState.selectedProvider

        if provider == .demo {
            try await streamDemo(text: text, tone: tone, instruction: customInstruction, onUpdate: onUpdate)
            return
        }

        let apiKey = appState.rewriteAPIKey
        guard !apiKey.isEmpty else {
            throw RewriteError.missingAPIKey(provider.displayName)
        }

        let model = appState.modelName.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !model.isEmpty else {
            throw RewriteError.invalidConfiguration("Choose a \(provider.displayName) model in Settings.")
        }

        let prompt = Self.rewritePrompt(
            text: text,
            tone: tone,
            customInstruction: customInstruction,
            context: context
        )

        do {
            try await performProviderRewrite(
                provider: provider,
                apiKey: apiKey,
                model: model,
                prompt: prompt,
                onUpdate: onUpdate
            )
        } catch let error as URLError {
            throw Self.mapNetworkError(error, providerName: provider.displayName)
        }
    }

    private func performProviderRewrite(
        provider: LLMProvider,
        apiKey: String,
        model: String,
        prompt: String,
        onUpdate: @escaping (String) -> Void
    ) async throws {
        switch provider {
        case .openAI:
            try await streamOpenAI(prompt: prompt, apiKey: apiKey, model: model, onUpdate: onUpdate)
        case .groq:
            try await streamChatCompletions(
                prompt: prompt,
                apiKey: apiKey,
                model: model,
                endpoint: URL(string: "https://api.groq.com/openai/v1/chat/completions")!,
                providerName: provider.displayName,
                onUpdate: onUpdate
            )
        case .cerebras:
            try await streamChatCompletions(
                prompt: prompt,
                apiKey: apiKey,
                model: model,
                endpoint: URL(string: "https://api.cerebras.ai/v1/chat/completions")!,
                providerName: provider.displayName,
                onUpdate: onUpdate
            )
        case .anthropic:
            try await streamAnthropic(prompt: prompt, apiKey: apiKey, model: model, onUpdate: onUpdate)
        case .demo:
            throw RewriteError.invalidConfiguration("On-device demo does not need a connection test.")
        }
    }

    private func streamOpenAI(
        prompt: String,
        apiKey: String,
        model: String,
        onUpdate: @escaping (String) -> Void
    ) async throws {
        var request = URLRequest(url: URL(string: "https://api.openai.com/v1/responses")!)
        request.httpMethod = "POST"
        request.setValue("Bearer \(apiKey)", forHTTPHeaderField: "Authorization")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try JSONSerialization.data(withJSONObject: [
            "model": model,
            "stream": true,
            "input": [
                ["role": "system", "content": "You are a precise writing assistant. Return only the rewritten text."],
                ["role": "user", "content": prompt]
            ]
        ])

        let (bytes, response) = try await URLSession.shared.bytes(for: request)
        try await validate(response: response, bytes: bytes, providerName: "OpenAI")

        var currentText = ""
        for try await line in bytes.lines {
            try Task.checkCancellation()
            guard line.hasPrefix("data: ") else { continue }
            let payload = String(line.dropFirst(6))
            guard let json = Self.jsonObject(payload) else { continue }
            let type = json["type"] as? String
            if type == "response.output_text.delta", let delta = json["delta"] as? String {
                currentText += delta
                onUpdate(currentText)
            } else if type == "error" {
                throw RewriteError.apiError(Self.errorMessage(from: json, fallback: "OpenAI streaming error."))
            }
        }
        guard !currentText.isEmpty else { throw RewriteError.emptyResponse }
    }

    private func streamChatCompletions(
        prompt: String,
        apiKey: String,
        model: String,
        endpoint: URL,
        providerName: String,
        onUpdate: @escaping (String) -> Void
    ) async throws {
        var request = URLRequest(url: endpoint)
        request.httpMethod = "POST"
        request.setValue("Bearer \(apiKey)", forHTTPHeaderField: "Authorization")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try JSONSerialization.data(withJSONObject: [
            "model": model,
            "stream": true,
            "messages": [
                ["role": "system", "content": "You are a precise writing assistant. Return only the rewritten text."],
                ["role": "user", "content": prompt]
            ]
        ])

        let (bytes, response) = try await URLSession.shared.bytes(for: request)
        try await validate(response: response, bytes: bytes, providerName: providerName)

        var currentText = ""
        for try await line in bytes.lines {
            try Task.checkCancellation()
            guard line.hasPrefix("data: ") else { continue }
            let payload = String(line.dropFirst(6))
            if payload == "[DONE]" { break }
            guard let json = Self.jsonObject(payload),
                  let choices = json["choices"] as? [[String: Any]],
                  let delta = choices.first?["delta"] as? [String: Any],
                  let content = delta["content"] as? String else { continue }
            currentText += content
            onUpdate(currentText)
        }
        guard !currentText.isEmpty else { throw RewriteError.emptyResponse }
    }

    private func streamAnthropic(
        prompt: String,
        apiKey: String,
        model: String,
        onUpdate: @escaping (String) -> Void
    ) async throws {
        var request = URLRequest(url: URL(string: "https://api.anthropic.com/v1/messages")!)
        request.httpMethod = "POST"
        request.setValue(apiKey, forHTTPHeaderField: "x-api-key")
        request.setValue("2023-06-01", forHTTPHeaderField: "anthropic-version")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try JSONSerialization.data(withJSONObject: [
            "model": model,
            "max_tokens": 1024,
            "stream": true,
            "messages": [["role": "user", "content": prompt]]
        ])

        let (bytes, response) = try await URLSession.shared.bytes(for: request)
        try await validate(response: response, bytes: bytes, providerName: "Anthropic")

        var currentText = ""
        for try await line in bytes.lines {
            try Task.checkCancellation()
            guard line.hasPrefix("data: "),
                  let json = Self.jsonObject(String(line.dropFirst(6))),
                  let type = json["type"] as? String else { continue }

            if type == "content_block_delta",
               let delta = json["delta"] as? [String: Any],
               let textDelta = delta["text"] as? String {
                currentText += textDelta
                onUpdate(currentText)
            } else if type == "error" {
                throw RewriteError.apiError(Self.errorMessage(from: json, fallback: "Anthropic streaming error."))
            } else if type == "message_stop" {
                break
            }
        }
        guard !currentText.isEmpty else { throw RewriteError.emptyResponse }
    }

    private func validate(
        response: URLResponse,
        bytes: URLSession.AsyncBytes,
        providerName: String
    ) async throws {
        guard let http = response as? HTTPURLResponse else {
            throw RewriteError.apiError("\(providerName) returned an invalid network response.")
        }
        guard (200..<300).contains(http.statusCode) else {
            var body = ""
            for try await line in bytes.lines { body += line }
            let json = Self.jsonObject(body)

            throw Self.mapHTTPError(statusCode: http.statusCode, providerName: providerName, json: json)
        }
    }

    private func streamDemo(
        text: String,
        tone: String,
        instruction: String?,
        onUpdate: @escaping (String) -> Void
    ) async throws {
        let demo = Self.demoRewrite(text: text, tone: tone, instruction: instruction)
        var streamed = ""
        for word in demo.split(separator: " ") {
            try Task.checkCancellation()
            try await Task.sleep(for: .milliseconds(22))
            streamed += (streamed.isEmpty ? "" : " ") + word
            onUpdate(streamed)
        }
    }

    // MARK: - Error Mapping Helpers

    internal static func mapNetworkError(_ error: URLError, providerName: String) -> RewriteError {
        switch error.code {
        case .notConnectedToInternet:
            return .apiError("You appear to be offline. Check your internet connection.")
        case .timedOut:
            return .apiError("The request to \(providerName) timed out.")
        default:
            return .apiError("Network error: \(error.localizedDescription)")
        }
    }

    internal static func mapHTTPError(statusCode: Int, providerName: String, json: [String: Any]? = nil) -> RewriteError {
        let fallback: String
        switch statusCode {
        case 401:
            return .apiError(
                "\(providerName) rejected this API key. Open Settings, enter an active \(providerName) key, and update the remembered key."
            )
        case 429:
            fallback = "\(providerName) rate limit reached (429). Try again shortly."
        case 500...599:
            fallback = "\(providerName) is experiencing server issues (\(statusCode)). Try again later."
        default:
            fallback = "\(providerName) request failed (\(statusCode))."
        }
        return .apiError(Self.errorMessage(from: json, fallback: fallback))
    }

    private static func rewritePrompt(
        text: String,
        tone: String,
        customInstruction: String?,
        context: String?
    ) -> String {
        var prompt = "Rewrite the following text."
        if let context, !context.isEmpty {
            prompt += "\nConversation context:\n<context>\n\(context)\n</context>"
        }
        if tone == "custom", let customInstruction, !customInstruction.isEmpty {
            prompt += "\nInstruction: \(customInstruction)"
        } else {
            switch tone {
            case "improve": prompt += "\nMake it professional, natural, and clear."
            case "concise": prompt += "\nMake it concise and direct."
            case "friendly": prompt += "\nMake it warm and approachable."
            case "expand": prompt += "\nExpand it with useful detail without inventing facts."
            default: break
            }
        }
        prompt += "\nReturn only the rewritten text: no markdown, quotation marks, labels, or commentary.\n\nText:\n\(text)"
        return prompt
    }

    private static func jsonObject(_ text: String) -> [String: Any]? {
        guard let data = text.data(using: .utf8) else { return nil }
        return try? JSONSerialization.jsonObject(with: data) as? [String: Any]
    }

    private static func errorMessage(from json: [String: Any]?, fallback: String) -> String {
        guard let json else { return fallback }
        if let error = json["error"] as? [String: Any], let message = error["message"] as? String {
            return message
        }
        if let message = json["message"] as? String { return message }
        return fallback
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
        case "concise": return polished.replacingOccurrences(of: "I have ", with: "I ")
        case "friendly": return "Just a quick note: \(polished)"
        case "expand": return "\(polished) Please let me know if you would like any additional detail."
        case "custom" where !(instruction ?? "").isEmpty: return polished
        default: return polished
        }
    }
}

import Foundation

enum RewriteError: LocalizedError, Equatable {
    case missingAPIKey(String)
    case invalidConfiguration(String)
    case invalidKey(String)
    case expiredOrRevokedKey(String)
    case wrongModel(String)
    case rateLimit(String)
    case offline
    case networkFailure(String)
    case providerOutage(String)
    case malformedResponse(String)
    case emptyResponse
    case apiError(String)

    var errorDescription: String? {
        switch self {
        case .missingAPIKey(let provider):
            return "No \(provider) API key is active. Add one in Settings or choose On-device demo."
        case .invalidConfiguration(let message), .apiError(let message), .networkFailure(let message):
            return Self.sanitize(message)
        case .invalidKey(let provider):
            return "\(provider) rejected this API key. Open Settings, enter a valid key, and update the remembered key."
        case .expiredOrRevokedKey(let provider):
            return "This \(provider) API key is expired or revoked. Replace it in Settings and update the remembered key."
        case .wrongModel(let provider):
            return "\(provider) does not recognize this model. Choose a valid model for \(provider) in Settings."
        case .rateLimit(let provider):
            return "\(provider) rate limit reached. Try again shortly."
        case .offline:
            return "You appear to be offline. Check your internet connection."
        case .providerOutage(let provider):
            return "\(provider) is experiencing an outage. Try again later."
        case .malformedResponse(let provider):
            return "\(provider) returned a malformed response. Try again or choose another model."
        case .emptyResponse:
            return "The provider returned no rewritten text. Try again or choose another model."
        }
    }

    /// Strips credential-shaped tokens from any user-visible error text.
    static func sanitize(_ message: String) -> String {
        var result = message
        let patterns = [
            #"sk-ant-[A-Za-z0-9_\-]+"#,
            #"sk-[A-Za-z0-9_\-]+"#,
            #"gsk_[A-Za-z0-9_\-]+"#,
            #"csk-[A-Za-z0-9_\-]+"#,
            #"(?i)bearer\s+[A-Za-z0-9_\-\.=]+"#,
            #"(?i)(api[_-]?key|x-api-key)\s*[:=]\s*\S+"#
        ]
        for pattern in patterns {
            if let regex = try? NSRegularExpression(pattern: pattern) {
                let range = NSRange(result.startIndex..<result.endIndex, in: result)
                result = regex.stringByReplacingMatches(in: result, range: range, withTemplate: "[redacted]")
            }
        }
        return result
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
        if let modelError = provider.modelValidationError(for: model) {
            throw RewriteError.invalidConfiguration(modelError)
        }
        if let keyError = provider.apiKeyFormatError(for: apiKey) {
            throw RewriteError.invalidConfiguration(keyError)
        }

        var receivedText = false
        let prompt = Self.rewritePrompt(
            text: "Polishly connection test.",
            tone: "concise",
            customInstruction: nil,
            context: nil
        )
        do {
            try await performProviderRewrite(
                provider: provider,
                apiKey: apiKey,
                model: model,
                prompt: prompt,
                maxTokens: 64
            ) { text in
                receivedText = receivedText || !text.isEmpty
            }
        } catch let error as RewriteError {
            throw error
        } catch let error as URLError {
            throw Self.mapNetworkError(error, providerName: provider.displayName)
        } catch {
            throw RewriteError.networkFailure("Could not reach \(provider.displayName). Check your connection and try again.")
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

        guard appState.providerIsReady else {
            if appState.rewriteAPIKey.isEmpty {
                throw RewriteError.missingAPIKey(provider.displayName)
            }
            if let configError = appState.providerConfigurationError {
                throw RewriteError.invalidConfiguration(configError)
            }
            throw RewriteError.invalidConfiguration("\(provider.displayName) is not ready. Check Settings.")
        }

        let apiKey = appState.rewriteAPIKey
        let model = appState.modelName.trimmingCharacters(in: .whitespacesAndNewlines)

        let prompt = Self.rewritePrompt(
            text: text,
            tone: tone,
            customInstruction: customInstruction,
            context: context
        )
        let maxTokens = Self.maxOutputTokens(for: text)

        do {
            // A network layer that stalls without ever closing the stream or
            // throwing (a wedged proxy, a server that stops sending bytes
            // mid-response) would otherwise leave isStreaming — and every
            // button gated on it — stuck forever with no way to recover but
            // force-quitting the app. Race the real request against a wall
            // clock so the UI always gets back control.
            try await withThrowingTaskGroup(of: Void.self) { group in
                group.addTask {
                    try await self.performProviderRewrite(
                        provider: provider,
                        apiKey: apiKey,
                        model: model,
                        prompt: prompt,
                        maxTokens: maxTokens,
                        onUpdate: onUpdate
                    )
                }
                group.addTask {
                    try await Task.sleep(for: .seconds(45))
                    throw RewriteError.apiError(
                        "\(provider.displayName) did not finish responding in time. Check your connection and try again."
                    )
                }
                defer { group.cancelAll() }
                try await group.next()
            }
        } catch let error as URLError {
            throw Self.mapNetworkError(error, providerName: provider.displayName)
        }
    }

    private func performProviderRewrite(
        provider: LLMProvider,
        apiKey: String,
        model: String,
        prompt: String,
        maxTokens: Int,
        onUpdate: @escaping (String) -> Void
    ) async throws {
        switch provider {
        case .openAI:
            try await streamOpenAI(prompt: prompt, apiKey: apiKey, model: model, maxTokens: maxTokens, onUpdate: onUpdate)
        case .groq:
            try await streamChatCompletions(
                prompt: prompt,
                apiKey: apiKey,
                model: model,
                maxTokens: maxTokens,
                endpoint: URL(string: "https://api.groq.com/openai/v1/chat/completions")!,
                providerName: provider.displayName,
                onUpdate: onUpdate
            )
        case .cerebras:
            try await streamChatCompletions(
                prompt: prompt,
                apiKey: apiKey,
                model: model,
                maxTokens: maxTokens,
                endpoint: URL(string: "https://api.cerebras.ai/v1/chat/completions")!,
                providerName: provider.displayName,
                onUpdate: onUpdate
            )
        case .anthropic:
            try await streamAnthropic(prompt: prompt, apiKey: apiKey, model: model, maxTokens: maxTokens, onUpdate: onUpdate)
        case .demo:
            throw RewriteError.invalidConfiguration("On-device demo does not need a connection test.")
        }
    }

    /// A rewrite's output is usually close to the input's length, but tone
    /// changes like "expand" or a custom "make this much longer" instruction
    /// can ask for substantially more. Scale the cap with input size instead
    /// of using one fixed number, so short selections don't reserve an
    /// unnecessarily large budget and long selections aren't cut off.
    private static func maxOutputTokens(for text: String) -> Int {
        let estimatedInputTokens = max(1, text.count / 4)
        return min(8192, max(2048, estimatedInputTokens * 8))
    }

    private func streamOpenAI(
        prompt: String,
        apiKey: String,
        model: String,
        maxTokens: Int,
        onUpdate: @escaping (String) -> Void
    ) async throws {
        var request = URLRequest(url: URL(string: "https://api.openai.com/v1/responses")!)
        request.httpMethod = "POST"
        request.setValue("Bearer \(apiKey)", forHTTPHeaderField: "Authorization")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try JSONSerialization.data(withJSONObject: [
            "model": model,
            "stream": true,
            "max_output_tokens": maxTokens,
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
                throw Self.mapProviderPayloadError(json: json, providerName: "OpenAI", fallback: "OpenAI streaming error.")
            }
        }
        guard !currentText.isEmpty else { throw RewriteError.emptyResponse }
    }

    private func streamChatCompletions(
        prompt: String,
        apiKey: String,
        model: String,
        maxTokens: Int,
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
            "max_tokens": maxTokens,
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
        maxTokens: Int,
        onUpdate: @escaping (String) -> Void
    ) async throws {
        var request = URLRequest(url: URL(string: "https://api.anthropic.com/v1/messages")!)
        request.httpMethod = "POST"
        request.setValue(apiKey, forHTTPHeaderField: "x-api-key")
        request.setValue("2023-06-01", forHTTPHeaderField: "anthropic-version")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try JSONSerialization.data(withJSONObject: [
            "model": model,
            "max_tokens": maxTokens,
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
                throw Self.mapProviderPayloadError(json: json, providerName: "Anthropic", fallback: "Anthropic streaming error.")
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
            throw RewriteError.malformedResponse(providerName)
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
        case .notConnectedToInternet, .networkConnectionLost, .dataNotAllowed:
            return .offline
        case .timedOut, .cannotFindHost, .cannotConnectToHost, .dnsLookupFailed:
            return .networkFailure("The request to \(providerName) failed. Check your connection and try again.")
        default:
            return .networkFailure("Network error talking to \(providerName). Check your connection and try again.")
        }
    }

    internal static func mapHTTPError(statusCode: Int, providerName: String, json: [String: Any]? = nil) -> RewriteError {
        let providerText = Self.errorMessage(from: json, fallback: "").lowercased()
        let looksLikeExpired = providerText.contains("expired") || providerText.contains("revoked")
        let looksLikeWrongModel =
            providerText.contains("model")
            && (providerText.contains("not found")
                || providerText.contains("does not exist")
                || providerText.contains("invalid")
                || providerText.contains("unknown")
                || providerText.contains("not available"))

        switch statusCode {
        case 401:
            return looksLikeExpired ? .expiredOrRevokedKey(providerName) : .invalidKey(providerName)
        case 403:
            return looksLikeExpired ? .expiredOrRevokedKey(providerName) : .invalidKey(providerName)
        case 404 where looksLikeWrongModel:
            return .wrongModel(providerName)
        case 400, 404, 422:
            if looksLikeWrongModel { return .wrongModel(providerName) }
            if providerText.contains("api key") || providerText.contains("authentication") {
                return .invalidKey(providerName)
            }
            return .apiError("\(providerName) rejected this request (\(statusCode)). Check the model and try again.")
        case 429:
            return .rateLimit(providerName)
        case 500...599:
            return .providerOutage(providerName)
        default:
            return .apiError("\(providerName) request failed (\(statusCode)).")
        }
    }

    internal static func mapProviderPayloadError(
        json: [String: Any]?,
        providerName: String,
        fallback: String
    ) -> RewriteError {
        let text = errorMessage(from: json, fallback: fallback).lowercased()
        if text.contains("model") && (text.contains("not found") || text.contains("invalid") || text.contains("unknown")) {
            return .wrongModel(providerName)
        }
        if text.contains("rate limit") || text.contains("too many requests") {
            return .rateLimit(providerName)
        }
        if text.contains("expired") || text.contains("revoked") {
            return .expiredOrRevokedKey(providerName)
        }
        if text.contains("api key") || text.contains("authentication") || text.contains("unauthorized") {
            return .invalidKey(providerName)
        }
        return .malformedResponse(providerName)
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
        guard let json else { return RewriteError.sanitize(fallback) }
        if let error = json["error"] as? [String: Any], let message = error["message"] as? String {
            return RewriteError.sanitize(message)
        }
        if let message = json["message"] as? String {
            return RewriteError.sanitize(message)
        }
        return RewriteError.sanitize(fallback)
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

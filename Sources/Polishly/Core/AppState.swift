import Foundation
import Combine
import SwiftUI
import AppKit
import ApplicationServices

enum LLMProvider: String, CaseIterable, Identifiable {
    case demo
    case openAI
    case groq
    case cerebras
    case anthropic

    var id: String { rawValue }

    var displayName: String {
        switch self {
        case .demo: return "On-device demo"
        case .openAI: return "OpenAI"
        case .groq: return "Groq"
        case .cerebras: return "Cerebras"
        case .anthropic: return "Anthropic"
        }
    }

    var shortName: String {
        self == .demo ? "Demo" : displayName
    }

    var defaultModel: String {
        switch self {
        case .demo: return "Local rules"
        case .openAI: return "gpt-5.2"
        case .groq: return "llama-3.3-70b-versatile"
        case .cerebras: return "gpt-oss-120b"
        case .anthropic: return "claude-haiku-4-5"
        }
    }

    var keyPlaceholder: String {
        switch self {
        case .demo: return ""
        case .openAI: return "sk-..."
        case .groq: return "gsk_..."
        case .cerebras: return "csk-..."
        case .anthropic: return "sk-ant-..."
        }
    }

    var privacyDescription: String {
        switch self {
        case .demo:
            return "Runs locally and never sends selected text off your Mac."
        default:
            return "Only text you explicitly rewrite is sent to \(displayName)."
        }
    }

    /// Returns a user-facing error when the model string is invalid for this provider.
    func modelValidationError(for model: String) -> String? {
        let trimmed = model.trimmingCharacters(in: .whitespacesAndNewlines)
        guard self != .demo else { return nil }
        guard !trimmed.isEmpty else {
            return "Choose a \(displayName) model in Settings."
        }
        guard trimmed.count <= 128 else {
            return "That model name is too long for \(displayName)."
        }
        guard !trimmed.contains(where: { $0.isNewline || $0 == " " }) else {
            return "Model names cannot contain spaces. Enter a single \(displayName) model id."
        }
        // Reject credential-shaped strings pasted into the model field.
        let lower = trimmed.lowercased()
        if lower.hasPrefix("sk-") || lower.hasPrefix("gsk_") || lower.hasPrefix("csk-") || lower.hasPrefix("sk-ant-") {
            return "That looks like an API key, not a model. Paste the key in the API Key field."
        }

        switch self {
        case .openAI:
            if lower.hasPrefix("claude") {
                return "OpenAI models do not use Claude ids. Try something like \(defaultModel)."
            }
            if !(lower.hasPrefix("gpt-") || lower.hasPrefix("o1") || lower.hasPrefix("o3")
                    || lower.hasPrefix("o4") || lower.hasPrefix("chatgpt-") || lower.hasPrefix("text-")) {
                return "OpenAI model ids usually start with gpt- or o1/o3/o4. Try \(defaultModel)."
            }
        case .anthropic:
            if !lower.hasPrefix("claude") {
                return "Anthropic model ids start with claude-. Try \(defaultModel)."
            }
        case .groq:
            if lower.hasPrefix("claude") {
                return "Groq does not serve Claude models. Try \(defaultModel)."
            }
        case .cerebras:
            if lower.hasPrefix("claude") {
                return "Cerebras does not serve Claude models. Try \(defaultModel)."
            }
        case .demo:
            break
        }
        return nil
    }

    /// Soft key-format check so obviously wrong pastes fail before a network call.
    func apiKeyFormatError(for key: String) -> String? {
        let trimmed = key.trimmingCharacters(in: .whitespacesAndNewlines)
        guard self != .demo else { return nil }
        guard !trimmed.isEmpty else {
            return "Enter a \(displayName) API key in Settings."
        }
        switch self {
        case .openAI:
            if trimmed.hasPrefix("sk-ant-") {
                return "That looks like an Anthropic key. Paste an OpenAI key (sk-…)."
            }
            if !trimmed.hasPrefix("sk-") {
                return "OpenAI API keys usually start with sk-."
            }
        case .anthropic:
            if !trimmed.hasPrefix("sk-ant-") {
                return "Anthropic API keys usually start with sk-ant-."
            }
        case .groq:
            if !trimmed.hasPrefix("gsk_") {
                return "Groq API keys usually start with gsk_."
            }
        case .cerebras:
            // Cerebras key prefixes have varied; only reject clearly foreign shapes.
            if trimmed.hasPrefix("sk-ant-") || trimmed.hasPrefix("gsk_") {
                return "That key looks like it belongs to another provider."
            }
        case .demo:
            break
        }
        return nil
    }
}

enum ProviderConnectionState {
    case idle
    case testing
    case connected
    case failed

    var systemImage: String {
        switch self {
        case .idle: return "info.circle"
        case .testing: return "clock"
        case .connected: return "checkmark.circle.fill"
        case .failed: return "exclamationmark.triangle.fill"
        }
    }
}

class AppState: ObservableObject {
    static let shared = AppState()

    /// Tests redirect Keychain traffic to an isolated service name so real keys stay untouched.
    static var keychainServiceOverrideForTesting: String?

    @Published private(set) var isAccessibilityTrusted = false
    @Published var apiKey = "" {
        didSet {
            if apiKey != oldValue {
                providerConnectionState = .idle
                if isTestingProviderConnection { isTestingProviderConnection = false }
            }
        }
    }
    @Published private(set) var hasRememberedAPIKey = false
    @Published var providerStatusMessage = ""
    @Published private(set) var isTestingProviderConnection = false
    @Published private(set) var providerConnectionState: ProviderConnectionState = .idle
    @Published var showOnboarding = false
    @Published var selectedProvider: LLMProvider {
        didSet {
            guard selectedProvider != oldValue else { return }
            UserDefaults.standard.set(selectedProvider.rawValue, forKey: "selectedProvider")
            clearInMemoryKeyState(statusMessage: "")
            modelName = Self.storedModel(for: selectedProvider)
            if selectedProvider == .demo {
                providerConnectionState = .connected
                providerStatusMessage = "On-device demo is ready — no network, no API key."
            } else {
                loadStoredAPIKeyAsync(allowInteraction: false, showMissingMessage: false)
            }
        }
    }
    @Published var modelName: String {
        didSet {
            UserDefaults.standard.set(modelName, forKey: Self.modelKey(for: selectedProvider))
            if modelName != oldValue {
                providerConnectionState = selectedProvider == .demo ? .connected : .idle
            }
        }
    }
    @Published var isPaused: Bool {
        didSet { UserDefaults.standard.set(isPaused, forKey: "isPaused") }
    }

    @Published var shortcutKeyCode: Int {
        didSet { UserDefaults.standard.set(shortcutKeyCode, forKey: "shortcutKeyCode") }
    }
    @Published var shortcutModifiers: Int {
        didSet { UserDefaults.standard.set(shortcutModifiers, forKey: "shortcutModifiers") }
    }

    var lastWorkingShortcutKeyCode: Int? {
        get { UserDefaults.standard.object(forKey: "lastWorkingShortcutKeyCode") as? Int }
        set { UserDefaults.standard.set(newValue, forKey: "lastWorkingShortcutKeyCode") }
    }

    var lastWorkingShortcutModifiers: Int? {
        get { UserDefaults.standard.object(forKey: "lastWorkingShortcutModifiers") as? Int }
        set { UserDefaults.standard.set(newValue, forKey: "lastWorkingShortcutModifiers") }
    }

    @Published var hasShortcutConflict = false

    private var cancellables = Set<AnyCancellable>()

    private init() {
        let savedProvider = UserDefaults.standard.string(forKey: "selectedProvider")
            .flatMap(LLMProvider.init(rawValue:)) ?? .demo
        self.selectedProvider = savedProvider
        self.modelName = Self.storedModel(for: savedProvider)
        self.isPaused = UserDefaults.standard.bool(forKey: "isPaused")

        // Default to Control+Option+Space (49 is Space, 6144 is controlKey+optionKey in Carbon).
        // A stored 0 for either value is not a real choice — 0 modifiers can never
        // register (ShortcutManager refuses a modifier-less shortcut) and keyCode 0
        // ("A") isn't one of the Picker's own options (Space/Return/R/P) — so both
        // only appear together from the same corruption. Validate them as a pair and
        // reset both rather than leave the app with no working shortcut, silently,
        // on every future launch.
        let storedModifiers = UserDefaults.standard.object(forKey: "shortcutModifiers") as? Int
        let storedKeyCode = UserDefaults.standard.object(forKey: "shortcutKeyCode") as? Int
        let validKeyCodes: Set<Int> = [49, 36, 15, 35] // Space, Return, R, P
        let isValid = (storedModifiers.map { $0 != 0 } ?? true)
            && (storedKeyCode.map { validKeyCodes.contains($0) } ?? true)
        if isValid {
            self.shortcutKeyCode = storedKeyCode ?? 49
            self.shortcutModifiers = storedModifiers ?? 6144
        } else {
            self.shortcutKeyCode = 49
            self.shortcutModifiers = 6144
            UserDefaults.standard.set(49, forKey: "shortcutKeyCode")
            UserDefaults.standard.set(6144, forKey: "shortcutModifiers")
            // The "last known working" fallback can carry the same corruption
            // forward the next time registration fails for a real reason.
            UserDefaults.standard.removeObject(forKey: "lastWorkingShortcutModifiers")
            UserDefaults.standard.removeObject(forKey: "lastWorkingShortcutKeyCode")
        }

        if savedProvider == .demo {
            providerConnectionState = .connected
            providerStatusMessage = "On-device demo is ready — no network, no API key."
        } else {
            loadStoredAPIKeyAsync(allowInteraction: false, showMissingMessage: false)
        }

        checkAccessibility()
        showOnboarding = !isAccessibilityTrusted

        // System Settings changes TCC state outside our process. Refresh on return
        // and while a permission window is open so the label cannot remain stale.
        NotificationCenter.default.publisher(for: NSApplication.didBecomeActiveNotification)
            .receive(on: RunLoop.main)
            .sink { [weak self] _ in self?.checkAccessibility() }
            .store(in: &cancellables)

        Timer.publish(every: 1, on: .main, in: .common)
            .autoconnect()
            .sink { [weak self] _ in self?.checkAccessibility() }
            .store(in: &cancellables)
    }

    /// A passive check never opens a system prompt; the user must explicitly request it.
    func checkAccessibility(requestPrompt: Bool = false) {
        let trusted: Bool
        if requestPrompt {
            let options = [kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true] as CFDictionary
            trusted = AXIsProcessTrustedWithOptions(options)
        } else {
            trusted = AXIsProcessTrusted()
        }
        // The 1 Hz poll must not republish an unchanged value; every observing
        // view would re-render each second.
        if trusted != isAccessibilityTrusted {
            isAccessibilityTrusted = trusted
        }
    }

    func openAccessibilitySettings() {
        checkAccessibility(requestPrompt: true)
        guard let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility") else { return }
        NSWorkspace.shared.open(url)
    }

    var rewriteAPIKey: String {
        selectedProvider == .demo ? "" : apiKey.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    /// Nil when demo, or when the active key + model are locally valid.
    var providerConfigurationError: String? {
        guard selectedProvider != .demo else { return nil }
        if let keyError = selectedProvider.apiKeyFormatError(for: rewriteAPIKey) {
            return keyError
        }
        if let modelError = selectedProvider.modelValidationError(for: modelName) {
            return modelError
        }
        return nil
    }

    var providerIsReady: Bool {
        selectedProvider == .demo || providerConfigurationError == nil
    }

    /// Writes only after the user expressly chooses to save their typed key.
    @discardableResult
    func saveAPIKey(testAfterSave: Bool = true) -> Bool {
        guard selectedProvider != .demo else { return false }
        if let configError = providerConfigurationError {
            providerConnectionState = .failed
            providerStatusMessage = configError
            return false
        }
        guard let data = rewriteAPIKey.data(using: .utf8) else { return false }
        let updating = hasRememberedAPIKey
        let saved = KeychainHelper.shared.save(
            data,
            service: Self.keychainService(for: selectedProvider),
            account: "user"
        )
        if saved {
            // Rotation: memory already holds the new key; Keychain now matches it.
            hasRememberedAPIKey = true
            providerConnectionState = .idle
            if testAfterSave {
                providerStatusMessage = updating
                    ? "Updated remembered key. Testing the provider connection…"
                    : "Saved securely. Testing the provider connection…"
                testProviderConnection()
            } else {
                providerStatusMessage = updating
                    ? "Updated remembered key."
                    : "Saved securely."
            }
        } else {
            providerConnectionState = .failed
            providerStatusMessage = "Could not save the key to Keychain."
        }
        return saved
    }

    func testProviderConnection() {
        guard selectedProvider != .demo, !isTestingProviderConnection else { return }
        if let configError = providerConfigurationError {
            providerConnectionState = .failed
            providerStatusMessage = configError
            return
        }
        let provider = selectedProvider
        let testedKey = rewriteAPIKey
        let testedModel = modelName.trimmingCharacters(in: .whitespacesAndNewlines)
        isTestingProviderConnection = true
        providerConnectionState = .testing
        providerStatusMessage = "Testing \(provider.displayName)…"

        Task { @MainActor [weak self] in
            do {
                try await RewriteClient.shared.validateConnection(
                    provider: provider,
                    apiKey: testedKey,
                    model: testedModel
                )
                guard let self else { return }
                self.isTestingProviderConnection = false
                guard self.selectedProvider == provider,
                      self.rewriteAPIKey == testedKey,
                      self.modelName.trimmingCharacters(in: .whitespacesAndNewlines) == testedModel else {
                    self.providerConnectionState = .idle
                    self.providerStatusMessage = "The key or model changed. Test the connection again."
                    return
                }
                self.providerConnectionState = .connected
                self.providerStatusMessage = "Connected to \(provider.displayName)."
            } catch {
                guard let self else { return }
                self.isTestingProviderConnection = false
                guard self.selectedProvider == provider,
                      self.rewriteAPIKey == testedKey,
                      self.modelName.trimmingCharacters(in: .whitespacesAndNewlines) == testedModel else {
                    self.providerConnectionState = .idle
                    self.providerStatusMessage = "The key or model changed. Test the connection again."
                    return
                }
                self.providerConnectionState = .failed
                self.providerStatusMessage = RewriteError.sanitize(error.localizedDescription)
            }
        }
    }

    /// Explicit fallback that may ask macOS to authorize Keychain access once.
    func loadStoredAPIKey() {
        providerStatusMessage = "Loading the saved \(selectedProvider.displayName) key…"
        guard selectedProvider != .demo else { return }
        let provider = selectedProvider
        let result = KeychainHelper.shared.read(
            service: Self.keychainService(for: provider),
            account: "user",
            allowInteraction: true
        )
        applyKeychainReadResult(
            result,
            provider: provider,
            allowInteraction: true,
            showMissingMessage: true
        )
    }

    /// Deletes the remembered key and clears in-memory credentials immediately.
    @discardableResult
    func forgetStoredAPIKey() -> Bool {
        guard selectedProvider != .demo else { return false }
        let deleted = KeychainHelper.shared.delete(
            service: Self.keychainService(for: selectedProvider),
            account: "user"
        )
        if deleted {
            clearInMemoryKeyState(
                statusMessage: "Forgot the saved \(selectedProvider.displayName) key."
            )
        } else {
            providerConnectionState = .failed
            providerStatusMessage = "Could not remove the saved key from Keychain."
        }
        return deleted
    }

    func resetModelToDefault() {
        modelName = selectedProvider.defaultModel
    }

    func isEnabled(for bundleIdentifier: String?) -> Bool {
        return AppCapabilityManager.shared.isEnabled(for: bundleIdentifier, isPaused: isPaused)
    }

    static func keychainService(for provider: LLMProvider) -> String {
        if let override = keychainServiceOverrideForTesting, !override.isEmpty {
            return override
        }
        return "com.polishly.apiKey.\(provider.rawValue)"
    }

    private func clearInMemoryKeyState(statusMessage: String) {
        apiKey = ""
        hasRememberedAPIKey = false
        isTestingProviderConnection = false
        providerConnectionState = .idle
        providerStatusMessage = statusMessage
    }

    private static func modelKey(for provider: LLMProvider) -> String {
        "model.\(provider.rawValue)"
    }

    private static func storedModel(for provider: LLMProvider) -> String {
        let value = UserDefaults.standard.string(forKey: modelKey(for: provider)) ?? ""
        return value.isEmpty ? provider.defaultModel : value
    }

    private func loadStoredAPIKeyAsync(allowInteraction: Bool, showMissingMessage: Bool) {
        guard selectedProvider != .demo else { return }
        let provider = selectedProvider
        let service = Self.keychainService(for: provider)

        DispatchQueue.global(qos: .userInitiated).async { [weak self] in
            let result = KeychainHelper.shared.read(
                service: service,
                account: "user",
                allowInteraction: allowInteraction
            )
            DispatchQueue.main.async {
                guard let self, self.selectedProvider == provider else { return }
                self.applyKeychainReadResult(
                    result,
                    provider: provider,
                    allowInteraction: allowInteraction,
                    showMissingMessage: showMissingMessage
                )
            }
        }
    }

    private func applyKeychainReadResult(
        _ result: KeychainReadResult,
        provider: LLMProvider,
        allowInteraction: Bool,
        showMissingMessage: Bool
    ) {
        switch result {
        case .success(let data):
            guard let key = String(data: data, encoding: .utf8), !key.isEmpty else {
                clearInMemoryKeyState(statusMessage: showMissingMessage ? "The saved key could not be read." : "")
                return
            }
            // Replace any stale in-memory value with the Keychain contents.
            apiKey = key
            hasRememberedAPIKey = true
            providerConnectionState = .idle
            providerStatusMessage = allowInteraction
                ? "Loaded the saved \(provider.displayName) key."
                : "Remembered key loaded automatically. Test the connection before rewriting."
        case .notFound:
            clearInMemoryKeyState(
                statusMessage: showMissingMessage ? "No saved \(provider.displayName) key was found." : ""
            )
        case .interactionRequired:
            // Keep whatever is already typed; do not loop password prompts.
            hasRememberedAPIKey = true
            providerConnectionState = .idle
            if showMissingMessage {
                providerStatusMessage = "macOS did not authorize access to the saved key."
            } else {
                providerStatusMessage = "Saved key needs one-time authorization. Choose Load Saved Key."
            }
        case .failure:
            providerConnectionState = .failed
            providerStatusMessage = "Could not read the saved key from Keychain."
        }
    }
}

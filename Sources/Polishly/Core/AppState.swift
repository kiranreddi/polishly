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

    @Published private(set) var isAccessibilityTrusted = false
    @Published var apiKey = "" {
        didSet {
            if apiKey != oldValue { providerConnectionState = .idle }
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
            apiKey = ""
            hasRememberedAPIKey = false
            providerStatusMessage = ""
            modelName = Self.storedModel(for: selectedProvider)
            if selectedProvider != .demo {
                loadStoredAPIKeyAsync(allowInteraction: false, showMissingMessage: false)
            }
        }
    }
    @Published var modelName: String {
        didSet {
            UserDefaults.standard.set(modelName, forKey: Self.modelKey(for: selectedProvider))
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

        if savedProvider != .demo {
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

    var providerIsReady: Bool {
        selectedProvider == .demo || (!rewriteAPIKey.isEmpty && !modelName.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
    }

    /// Writes only after the user expressly chooses to save their typed key.
    @discardableResult
    func saveAPIKey() -> Bool {
        guard selectedProvider != .demo,
              !rewriteAPIKey.isEmpty,
              let data = rewriteAPIKey.data(using: .utf8) else { return false }
        let saved = KeychainHelper.shared.save(
            data,
            service: Self.keychainService(for: selectedProvider),
            account: "user"
        )
        hasRememberedAPIKey = saved
        providerStatusMessage = saved
            ? "Saved securely. Testing the provider connection…"
            : "Could not save the key to Keychain."
        if saved { testProviderConnection() }
        return saved
    }

    func testProviderConnection() {
        guard selectedProvider != .demo, providerIsReady, !isTestingProviderConnection else { return }
        let provider = selectedProvider
        let testedKey = rewriteAPIKey
        let testedModel = modelName.trimmingCharacters(in: .whitespacesAndNewlines)
        isTestingProviderConnection = true
        providerConnectionState = .testing
        providerStatusMessage = "Testing \(provider.displayName) with \(testedModel)…"

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
                self.providerStatusMessage = "Connected to \(provider.displayName). The key and model are ready."
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
                self.providerStatusMessage = error.localizedDescription
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

    @discardableResult
    func forgetStoredAPIKey() -> Bool {
        guard selectedProvider != .demo else { return false }
        let deleted = KeychainHelper.shared.delete(
            service: Self.keychainService(for: selectedProvider),
            account: "user"
        )
        if deleted {
            apiKey = ""
            hasRememberedAPIKey = false
            providerStatusMessage = "Forgot the saved \(selectedProvider.displayName) key."
        } else {
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

    private static func keychainService(for provider: LLMProvider) -> String {
        "com.polishly.apiKey.\(provider.rawValue)"
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
                if showMissingMessage { providerStatusMessage = "The saved key could not be read." }
                return
            }
            apiKey = key
            hasRememberedAPIKey = true
            providerStatusMessage = allowInteraction
                ? "Loaded the saved \(provider.displayName) key."
                : "Remembered key loaded automatically. Test the connection before rewriting."
        case .notFound:
            if showMissingMessage {
                providerStatusMessage = "No saved \(provider.displayName) key was found."
            }
        case .interactionRequired:
            if showMissingMessage {
                providerStatusMessage = "macOS did not authorize access to the saved key."
            } else {
                providerStatusMessage = "Saved key needs one-time authorization. Choose Load Saved Key."
            }
        case .failure(let status):
            providerStatusMessage = "Keychain error \(status)."
        }
    }
}

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
        case .openAI: return "gpt-5.6-sol"
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

class AppState: ObservableObject {
    static let shared = AppState()

    @Published private(set) var isAccessibilityTrusted = false
    @Published var apiKey = ""
    @Published var providerStatusMessage = ""
    @Published var showOnboarding = false
    @Published var selectedProvider: LLMProvider {
        didSet {
            guard selectedProvider != oldValue else { return }
            UserDefaults.standard.set(selectedProvider.rawValue, forKey: "selectedProvider")
            apiKey = ""
            providerStatusMessage = ""
            modelName = Self.storedModel(for: selectedProvider)
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
    @Published var notesEnabled: Bool {
        didSet { UserDefaults.standard.set(notesEnabled, forKey: "notesEnabled") }
    }
    @Published var teamsEnabled: Bool {
        didSet { UserDefaults.standard.set(teamsEnabled, forKey: "teamsEnabled") }
    }

    private var cancellables = Set<AnyCancellable>()

    private init() {
        let savedProvider = UserDefaults.standard.string(forKey: "selectedProvider")
            .flatMap(LLMProvider.init(rawValue:)) ?? .demo
        self.selectedProvider = savedProvider
        self.modelName = Self.storedModel(for: savedProvider)
        self.isPaused = UserDefaults.standard.bool(forKey: "isPaused")
        self.notesEnabled = UserDefaults.standard.object(forKey: "notesEnabled") as? Bool ?? true
        self.teamsEnabled = UserDefaults.standard.object(forKey: "teamsEnabled") as? Bool ?? true

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
        if requestPrompt {
            let options = [kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true] as CFDictionary
            isAccessibilityTrusted = AXIsProcessTrustedWithOptions(options)
        } else {
            isAccessibilityTrusted = AXIsProcessTrusted()
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
        providerStatusMessage = saved
            ? "Saved \(selectedProvider.displayName) key to Keychain."
            : "Could not save the key to Keychain."
        return saved
    }

    /// Called only from an explicit settings action because reading a login-keychain
    /// item may require the user's macOS password.
    @discardableResult
    func loadStoredAPIKey() -> Bool {
        guard selectedProvider != .demo,
              let data = KeychainHelper.shared.read(
                service: Self.keychainService(for: selectedProvider),
                account: "user"
              ),
              let key = String(data: data, encoding: .utf8),
              !key.isEmpty else {
            providerStatusMessage = "No saved \(selectedProvider.displayName) key was found."
            return false
        }
        apiKey = key
        providerStatusMessage = "Loaded \(selectedProvider.displayName) key for this session."
        return true
    }

    func resetModelToDefault() {
        modelName = selectedProvider.defaultModel
    }

    func isEnabled(for bundleIdentifier: String?) -> Bool {
        guard !isPaused else { return false }
        switch bundleIdentifier {
        case "com.apple.Notes": return notesEnabled
        case "com.microsoft.teams2", "com.microsoft.teams": return teamsEnabled
        default: return true
        }
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
}

import Foundation
import Combine
import SwiftUI
import ApplicationServices

class AppState: ObservableObject {
    static let shared = AppState()
    
    @Published var isAccessibilityTrusted: Bool = false
    @Published var apiKey: String = "" {
        didSet {
            if let data = apiKey.data(using: .utf8) {
                KeychainHelper.shared.save(data, service: "com.polishly.apiKey", account: "user")
            }
        }
    }
    @Published var showOnboarding: Bool = false
    @Published var useDemoMode: Bool {
        didSet { UserDefaults.standard.set(useDemoMode, forKey: "useDemoMode") }
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
    
    private init() {
        self.isPaused = UserDefaults.standard.bool(forKey: "isPaused")
        self.notesEnabled = UserDefaults.standard.object(forKey: "notesEnabled") as? Bool ?? true
        self.teamsEnabled = UserDefaults.standard.object(forKey: "teamsEnabled") as? Bool ?? true
        // Keep new installs local until the user explicitly opts into using their key.
        self.useDemoMode = UserDefaults.standard.object(forKey: "useDemoMode") as? Bool ?? true
        checkAccessibility()
        
        // Demo mode works without credentials, so never unlock the user's login
        // Keychain merely to launch a menu-bar app.
        showOnboarding = !isAccessibilityTrusted
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

    func requestAccessibility() { checkAccessibility(requestPrompt: true) }

    var rewriteAPIKey: String {
        useDemoMode ? "" : apiKey
    }

    /// Called only from an explicit settings action because reading a login-keychain
    /// item may require the user's macOS password.
    @discardableResult
    func loadStoredAPIKey() -> Bool {
        guard let data = KeychainHelper.shared.read(service: "com.polishly.apiKey", account: "user"),
              let key = String(data: data, encoding: .utf8),
              !key.isEmpty else {
            return false
        }
        apiKey = key
        useDemoMode = false
        return true
    }

    func isEnabled(for bundleIdentifier: String?) -> Bool {
        guard !isPaused else { return false }
        switch bundleIdentifier {
        case "com.apple.Notes": return notesEnabled
        case "com.microsoft.teams2", "com.microsoft.teams": return teamsEnabled
        default: return true
        }
    }
}

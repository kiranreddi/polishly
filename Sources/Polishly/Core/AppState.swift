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
        if let data = KeychainHelper.shared.read(service: "com.polishly.apiKey", account: "user"),
           let key = String(data: data, encoding: .utf8) {
            self.apiKey = key
        }
        
        checkAccessibility()
        
        if apiKey.isEmpty || !isAccessibilityTrusted {
            showOnboarding = true
        }
    }
    
    func checkAccessibility() {
        let options = [kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true] as CFDictionary
        isAccessibilityTrusted = AXIsProcessTrustedWithOptions(options)
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

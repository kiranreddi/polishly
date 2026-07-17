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
    
    private init() {
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
}

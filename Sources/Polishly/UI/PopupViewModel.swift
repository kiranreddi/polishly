import Foundation
import Combine
import SwiftUI

class PopupViewModel: ObservableObject {
    @Published var hasContext: Bool = false
    @Published var contextMessage: String = "Using selected text"
    
    @Published var rewriteTitle: String = "Rewriting..."
    @Published var diffTokens: [DiffToken] = []
    
    @Published var isStreaming: Bool = false
    @Published var isError: Bool = false
    @Published var errorMessage: String = ""
    
    @Published var showReviseInput: Bool = false
    @Published var reviseText: String = ""
    
    @Published var selectedTab: String = "improve"
    
    private var originalText: String = ""
    private var currentTargetText: String = ""
    private var originalCapture: SelectionEngine.CapturedText?
    
    // Dependencies
    var closeAction: () -> Void = {}
    
    func configure(with capture: SelectionEngine.CapturedText) {
        self.originalText = capture.text
        self.originalCapture = capture
        
        // Mock checking context based on app
        if let app = NSWorkspace.shared.frontmostApplication {
            if app.bundleIdentifier == "com.microsoft.teams2" {
                self.hasContext = true
                self.contextMessage = "Using visible Teams messages as context"
            } else if app.bundleIdentifier == "com.apple.Notes" {
                self.hasContext = false
                self.contextMessage = "No thread context available in Notes"
            } else {
                self.hasContext = false
                self.contextMessage = "Using only your selected text"
            }
        }
        
        self.selectTab("improve")
    }
    
    func close() {
        closeAction()
    }
    
    func accept() {
        guard let capture = originalCapture, !currentTargetText.isEmpty else { return }
        SelectionEngine.shared.inject(text: currentTargetText, originalCapture: capture) { success in
            DispatchQueue.main.async {
                if success {
                    self.close()
                } else {
                    self.isError = true
                    self.errorMessage = "Failed to replace text."
                }
            }
        }
    }
    
    func selectTab(_ tab: String) {
        self.selectedTab = tab
        self.showReviseInput = false
        requestRewrite(tone: tab)
    }
    
    func regenerate() {
        if showReviseInput && !reviseText.isEmpty {
            submitRevise()
        } else {
            requestRewrite(tone: selectedTab)
        }
    }
    
    func submitRevise() {
        guard !reviseText.isEmpty else { return }
        self.selectedTab = "custom"
        self.showReviseInput = false
        requestRewrite(tone: "custom", customInstruction: reviseText)
    }
    
    private func requestRewrite(tone: String, customInstruction: String? = nil) {
        self.isStreaming = true
        self.isError = false
        self.diffTokens = []
        self.currentTargetText = ""
        
        switch tone {
        case "improve": self.rewriteTitle = "Rewritten for clarity"
        case "concise": self.rewriteTitle = "Tightened to the essentials"
        case "friendly": self.rewriteTitle = "Warmer tone, same content"
        case "expand": self.rewriteTitle = "Expanded with more detail"
        case "custom": self.rewriteTitle = "Custom: \"\(customInstruction ?? "")\""
        default: self.rewriteTitle = "Rewritten"
        }
        
        let contextText = self.hasContext ? "Previous messages as context..." : nil // In a real app we'd fetch actual context from UI
        
        Task {
            do {
                try await AnthropicClient.shared.rewriteStream(
                    text: originalText,
                    tone: tone,
                    customInstruction: customInstruction,
                    context: contextText
                ) { [weak self] currentText in
                    DispatchQueue.main.async {
                        guard let self = self else { return }
                        self.currentTargetText = currentText
                        self.diffTokens = DiffEngine.diffWords(original: self.originalText, target: currentText)
                    }
                }
                
                DispatchQueue.main.async {
                    self.isStreaming = false
                }
            } catch {
                DispatchQueue.main.async {
                    self.isStreaming = false
                    self.isError = true
                    self.errorMessage = error.localizedDescription
                }
            }
        }
    }
}

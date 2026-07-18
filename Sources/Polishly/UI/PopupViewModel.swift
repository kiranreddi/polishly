import Foundation
import Combine
import SwiftUI

@MainActor
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
    private var lastCustomInstruction: String?
    
    // Dependencies
    var closeAction: () -> Void = {}
    
    func configure(with capture: SelectionEngine.CapturedText) {
        self.originalText = capture.text
        self.originalCapture = capture
        
        // Promise B stays off until a per-app extractor has passed its own validation.
        self.hasContext = false
        self.contextMessage = "No thread context available — using only your selected text"
        
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

    func copy() {
        guard !currentTargetText.isEmpty else { return }
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(currentTargetText, forType: .string)
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

    func retry() {
        requestRewrite(tone: selectedTab, customInstruction: lastCustomInstruction)
    }
    
    func submitRevise() {
        guard !reviseText.isEmpty else { return }
        self.selectedTab = "custom"
        self.showReviseInput = false
        requestRewrite(tone: "custom", customInstruction: reviseText)
    }
    
    private func requestRewrite(tone: String, customInstruction: String? = nil) {
        lastCustomInstruction = customInstruction
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
        
        Task {
            do {
                try await AnthropicClient.shared.rewriteStream(
                    text: originalText,
                    tone: tone,
                    customInstruction: customInstruction,
                    context: nil
                ) { [weak self] currentText in
                    Task { @MainActor [weak self] in
                        guard let self = self else { return }
                        self.currentTargetText = currentText
                        self.diffTokens = DiffEngine.diffWords(original: self.originalText, target: currentText)
                    }
                }
                
                Task { @MainActor [weak self] in
                    guard let self else { return }
                    self.isStreaming = false
                }
            } catch {
                Task { @MainActor [weak self] in
                    guard let self else { return }
                    self.isStreaming = false
                    self.isError = true
                    self.errorMessage = error.localizedDescription
                }
            }
        }
    }
}

import SwiftUI
import KeyboardShortcuts

struct OnboardingView: View {
    @ObservedObject var appState = AppState.shared
    
    var body: some View {
        VStack(spacing: 24) {
            Text("Welcome to Polishly")
                .font(.largeTitle)
                .fontWeight(.bold)
            
            Text("Polishly uses AI to rewrite text anywhere on your Mac. To do this, it needs Accessibility permission to read your selected text and paste the rewrites.")
                .multilineTextAlignment(.center)
                .padding(.horizontal)
            
            if !appState.isAccessibilityTrusted {
                VStack(spacing: 12) {
                    Text("1. Grant Accessibility Access")
                        .font(.headline)
                    
                    Button("Open System Settings") {
                        appState.requestAccessibility()
                        let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility")!
                        NSWorkspace.shared.open(url)
                    }
                    .buttonStyle(.borderedProminent)
                    
                    Button("I've granted access") {
                        appState.checkAccessibility()
                    }
                    .buttonStyle(.bordered)
                }
                .padding()
                .background(Color(NSColor.controlBackgroundColor))
                .cornerRadius(12)
            } else {
                VStack(spacing: 12) {
                    Text("✅ Accessibility Granted")
                        .font(.headline)
                        .foregroundColor(.green)
                }
                .padding()
                .background(Color(NSColor.controlBackgroundColor))
                .cornerRadius(12)
            }
            
            VStack(spacing: 12) {
                Text("2. Add Anthropic API Key")
                    .font(.headline)
                
                SecureField("sk-ant-api03-...", text: $appState.apiKey)
                    .textFieldStyle(RoundedBorderTextFieldStyle())
                    .frame(maxWidth: 300)
            }
            .padding()
            .background(Color(NSColor.controlBackgroundColor))
            .cornerRadius(12)
            
            Button("Use demo mode") { appState.showOnboarding = false }
            .buttonStyle(.borderedProminent)

            Text("You can add a key later in Settings. Demo mode never sends text off your Mac.")
                .font(.caption)
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)
        }
        .padding(40)
        .frame(width: 500, height: 500)
    }
}

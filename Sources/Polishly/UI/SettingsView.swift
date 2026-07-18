import SwiftUI

struct SettingsView: View {
    @ObservedObject var appState = AppState.shared
    
    var body: some View {
        Form {
            Section(header: Text("API Configuration")) {
                SecureField("Anthropic API Key", text: $appState.apiKey)
                    .textFieldStyle(RoundedBorderTextFieldStyle())
                    .onChange(of: appState.apiKey) { _, _ in
                        appState.useDemoMode = false
                    }
                Text("Your key is stored securely in the macOS Keychain and only used for rewriting text when you invoke Polishly.")
                    .font(.caption)
                    .foregroundColor(.secondary)
                if appState.apiKey.isEmpty {
                    Button("Load stored key…") {
                        appState.loadStoredAPIKey()
                    }
                    Text("This may ask macOS to unlock your login Keychain.")
                        .font(.caption)
                        .foregroundColor(.secondary)
                }
                Toggle("Use local demo rewrites", isOn: $appState.useDemoMode)
                Text("Demo mode never sends selected text off your Mac.")
                    .font(.caption)
                    .foregroundColor(.secondary)
            }
            
            Section(header: Text("Keyboard Shortcut")) {
                LabeledContent("Rewrite Selection") {
                    Text("Control–Option–Space")
                        .foregroundColor(.secondary)
                }
                Text("This shortcut is consumed before it reaches the active app, so the selected text stays intact.")
                    .font(.caption)
                    .foregroundColor(.secondary)
            }

            Section(header: Text("Activation")) {
                Toggle("Pause Polishly", isOn: $appState.isPaused)
                Toggle("Enable in Notes", isOn: $appState.notesEnabled)
                Toggle("Enable in Microsoft Teams", isOn: $appState.teamsEnabled)
                Text("Thread context is disabled until each app has a separately validated extractor.")
                    .font(.caption)
                    .foregroundColor(.secondary)
            }
            
            Section(header: Text("Permissions")) {
                HStack {
                    Text("Accessibility Access:")
                    Spacer()
                    if appState.isAccessibilityTrusted {
                        Text("Granted")
                            .foregroundColor(.green)
                    } else {
                        Text("Not Granted")
                            .foregroundColor(.red)
                        Button("Check") {
                            appState.checkAccessibility()
                        }
                    }
                }
            }
        }
        .padding(20)
        .frame(width: 450, height: 410)
    }
}

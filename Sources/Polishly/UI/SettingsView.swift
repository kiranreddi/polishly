import SwiftUI
import KeyboardShortcuts

struct SettingsView: View {
    @ObservedObject var appState = AppState.shared
    
    var body: some View {
        Form {
            Section(header: Text("API Configuration")) {
                SecureField("Anthropic API Key", text: $appState.apiKey)
                    .textFieldStyle(RoundedBorderTextFieldStyle())
                Text("Your key is stored securely in the macOS Keychain and only used for rewriting text when you invoke Polishly.")
                    .font(.caption)
                    .foregroundColor(.secondary)
            }
            
            Section(header: Text("Keyboard Shortcut")) {
                KeyboardShortcuts.Recorder("Rewrite Selection:", name: .rewrite)
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

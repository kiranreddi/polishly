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
        .frame(width: 450, height: 300)
    }
}

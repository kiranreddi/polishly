import SwiftUI

struct SettingsView: View {
    @ObservedObject var appState = AppState.shared

    var body: some View {
        Form {
            providerSection

            Section("Keyboard Shortcut") {
                LabeledContent("Rewrite Selection") {
                    Text("Control–Option–Space")
                        .foregroundStyle(.secondary)
                }
                Text("Polishly only reads selected text after you press the shortcut.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            Section("Activation") {
                Toggle("Pause Polishly", isOn: $appState.isPaused)
                Toggle("Enable in Notes", isOn: $appState.notesEnabled)
                Toggle("Enable in Microsoft Teams", isOn: $appState.teamsEnabled)
            }

            Section("Permissions") {
                HStack(spacing: 10) {
                    Image(systemName: appState.isAccessibilityTrusted ? "checkmark.circle.fill" : "exclamationmark.circle.fill")
                        .foregroundStyle(appState.isAccessibilityTrusted ? .green : .orange)
                    VStack(alignment: .leading, spacing: 2) {
                        Text("Accessibility")
                        Text(appState.isAccessibilityTrusted ? "Access granted" : "Access is still needed")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                    Spacer()
                    if !appState.isAccessibilityTrusted {
                        Button("Open Settings") {
                            appState.openAccessibilitySettings()
                        }
                    }
                    Button("Refresh") {
                        appState.checkAccessibility()
                    }
                }
                Text("This status refreshes automatically when you return from System Settings.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
        }
        .formStyle(.grouped)
        .padding(8)
        .frame(width: 500, height: 700)
        .onAppear { appState.checkAccessibility() }
    }

    @ViewBuilder
    private var providerSection: some View {
        Section("Rewrite Provider") {
            Picker("Provider", selection: $appState.selectedProvider) {
                ForEach(LLMProvider.allCases) { provider in
                    Text(provider.displayName).tag(provider)
                }
            }

            if appState.selectedProvider == .demo {
                Label("Ready — no API key required", systemImage: "checkmark.circle.fill")
                    .foregroundStyle(.green)
                Text(appState.selectedProvider.privacyDescription)
                    .font(.caption)
                    .foregroundStyle(.secondary)
            } else {
                HStack {
                    TextField("Model", text: $appState.modelName)
                        .textFieldStyle(.roundedBorder)
                    Button("Default") {
                        appState.resetModelToDefault()
                    }
                }

                LabeledContent("API Key") {
                    SecureField(appState.selectedProvider.keyPlaceholder, text: $appState.apiKey)
                        .labelsHidden()
                        .textFieldStyle(.roundedBorder)
                }

                HStack {
                    Button("Save to Keychain…") {
                        appState.saveAPIKey()
                    }
                    .disabled(!appState.providerIsReady)

                    Button("Load Saved Key…") {
                        appState.loadStoredAPIKey()
                    }
                }

                if !appState.providerStatusMessage.isEmpty {
                    Label(appState.providerStatusMessage, systemImage: appState.providerIsReady ? "checkmark.circle" : "info.circle")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }

                Text(appState.selectedProvider.privacyDescription)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                Text("A typed key is active for this session and stays in memory unless you explicitly save it. Saving or loading may show a normal macOS Keychain prompt.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
        }
    }
}

import SwiftUI

struct SettingsView: View {
    @ObservedObject var appState = AppState.shared

    var body: some View {
        Form {
            providerSection

            Section("Keyboard Shortcut") {
                LabeledContent("Modifiers") {
                    HStack {
                        Toggle("Control", isOn: Binding(
                            get: { (appState.shortcutModifiers & 4096) != 0 },
                            set: { if $0 { appState.shortcutModifiers |= 4096 } else { appState.shortcutModifiers &= ~4096 } }
                        ))
                        Toggle("Option", isOn: Binding(
                            get: { (appState.shortcutModifiers & 2048) != 0 },
                            set: { if $0 { appState.shortcutModifiers |= 2048 } else { appState.shortcutModifiers &= ~2048 } }
                        ))
                        Toggle("Command", isOn: Binding(
                            get: { (appState.shortcutModifiers & 256) != 0 },
                            set: { if $0 { appState.shortcutModifiers |= 256 } else { appState.shortcutModifiers &= ~256 } }
                        ))
                    }
                }
                LabeledContent("Key") {
                    Picker("", selection: Binding(
                        get: { appState.shortcutKeyCode },
                        set: { appState.shortcutKeyCode = $0 }
                    )) {
                        Text("Space").tag(49)
                        Text("Return").tag(36)
                        Text("R").tag(15)
                        Text("P").tag(35)
                    }
                    .labelsHidden()
                }
                Button("Apply Shortcut") {
                    ShortcutManager.shared.registerGlobalShortcut()
                }

                if appState.hasShortcutConflict {
                    Label("Shortcut unavailable — pick at least one modifier, and a combination no other app or system feature already uses.", systemImage: "exclamationmark.triangle.fill")
                        .foregroundStyle(.orange)
                        .font(.caption)
                }

                Text("Polishly locally detects selections, but sends text only after trigger click/hotkey.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            Section("Activation") {
                Toggle("Pause Polishly globally", isOn: $appState.isPaused)
            }

            Section("Enabled Apps") {
                ForEach([
                    AppCapabilityManager.AppGroup.notes,
                    .teams,
                    .mail,
                    .outlook,
                    .slack,
                    .safari,
                    .chrome,
                    .edge
                ], id: \.self) { app in
                    let defaultsKey = "enabled_\(app.rawValue)"
                    Toggle(app.displayName, isOn: Binding(
                        get: { UserDefaults.standard.object(forKey: defaultsKey) as? Bool ?? true },
                        set: { UserDefaults.standard.set($0, forKey: defaultsKey) }
                    ))
                }

                Toggle("Other Apps", isOn: Binding(
                    get: { UserDefaults.standard.bool(forKey: "enabled_other_apps") },
                    set: { UserDefaults.standard.set($0, forKey: "enabled_other_apps") }
                ))
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
                    Button(appState.hasRememberedAPIKey ? "Update Remembered Key" : "Save & Remember Key") {
                        appState.saveAPIKey()
                    }
                    .disabled(!appState.providerIsReady)

                    Button(appState.isTestingProviderConnection ? "Testing…" : "Test Connection") {
                        appState.testProviderConnection()
                    }
                    .disabled(!appState.providerIsReady || appState.isTestingProviderConnection)

                    if appState.hasRememberedAPIKey {
                        Button("Forget Key…", role: .destructive) {
                            appState.forgetStoredAPIKey()
                        }
                    } else {
                        Button("Load Saved Key…") {
                            appState.loadStoredAPIKey()
                        }
                    }
                }

                if appState.hasRememberedAPIKey {
                    Label("Stored securely in Keychain — loads automatically", systemImage: "lock.fill")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }

                if !appState.providerStatusMessage.isEmpty {
                    Label(appState.providerStatusMessage, systemImage: appState.providerConnectionState.systemImage)
                        .font(.caption)
                        .foregroundStyle(providerStatusColor)
                }

                Text(appState.selectedProvider.privacyDescription)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                Text("Enter the key once, then choose Save & Remember Key. Polishly will securely reload it on future launches without repeatedly asking for it.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
        }
    }

    private var providerStatusColor: Color {
        switch appState.providerConnectionState {
        case .connected: return .green
        case .failed: return .orange
        case .idle, .testing: return .secondary
        }
    }
}

import SwiftUI

struct OnboardingView: View {
    @ObservedObject var appState = AppState.shared

    var body: some View {
        VStack(spacing: 26) {
            Image(systemName: "text.badge.checkmark")
                .font(.system(size: 42, weight: .medium))
                .foregroundStyle(.tint)
                .accessibilityHidden(true)

            VStack(spacing: 8) {
                Text("Welcome to Polishly")
                    .font(.largeTitle.weight(.bold))
                    .accessibilityAddTraits(.isHeader)
                Text("Select text in Notes or Teams, press Control–Option–Space, and choose a clearer rewrite.")
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.center)
            }

            if appState.onboardingState == .accessibilityMissing || appState.onboardingState == .accessibilityGranted {
                accessibilityStep
            } else if appState.onboardingState == .providerMissing || appState.onboardingState == .providerConnected {
                providerStep
            }
        }
        .padding(40)
        .frame(width: 520)
        .onAppear { appState.checkAccessibility() }
    }

    private var accessibilityStep: some View {
        VStack(spacing: 26) {
            permissionCard

            VStack(spacing: 8) {
                Button("Continue") {
                    withAnimation {
                        // Land on a real provider by default so the setup
                        // fields are immediately meaningful; Skip still
                        // switches back to demo explicitly if that's chosen.
                        if appState.selectedProvider == .demo {
                            appState.selectedProvider = .openAI
                        }
                        appState.onboardingState = .providerMissing
                    }
                }
                .buttonStyle(.borderedProminent)
                .controlSize(.large)
                .disabled(appState.onboardingState != .accessibilityGranted)
                .keyboardShortcut(.defaultAction)
                .accessibilityLabel(appState.onboardingState == .accessibilityGranted ? "Continue to provider setup" : "Accessibility access required to continue")
            }
        }
    }

    private var permissionCard: some View {
        let isGranted = (appState.onboardingState == .accessibilityGranted)
        return VStack(alignment: .leading, spacing: 14) {
            HStack(spacing: 10) {
                Image(systemName: isGranted ? "checkmark.circle.fill" : "hand.raised.fill")
                    .foregroundStyle(isGranted ? .green : .orange)
                    .accessibilityHidden(true)
                VStack(alignment: .leading, spacing: 2) {
                    Text("Accessibility Access")
                        .font(.headline)
                    Text(isGranted ? "Granted — Polishly is ready to read selections." : "Required to read and replace only the text you select.")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                Spacer()
            }
            .accessibilityElement(children: .combine)
            .accessibilityValue(isGranted ? "Granted" : "Required")

            if !isGranted {
                HStack {
                    Button("Open System Settings") {
                        appState.openAccessibilitySettings()
                    }
                    .buttonStyle(.borderedProminent)
                    .accessibilityHint("Opens Privacy & Security in System Settings")
                }
                Text("The status updates automatically after permission is granted.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
        }
        .padding(18)
        .background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: 14))
    }

    private var providerStep: some View {
        VStack(spacing: 20) {
            VStack(alignment: .leading, spacing: 14) {
                Text("Connect a Provider")
                    .font(.headline)
                    .accessibilityAddTraits(.isHeader)
                Text("Add a real AI provider now, or skip and start with the on-device demo — you can change this anytime from the menu bar.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.leading)
            }
            .padding(18)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: 14))
            .accessibilityElement(children: .combine)

            providerSetupCard

            HStack(spacing: 12) {
                Button("Skip — Use Demo Mode") {
                    appState.selectedProvider = .demo
                    appState.onboardingState = .ready
                }
                .buttonStyle(.bordered)
                .controlSize(.large)
                .accessibilityLabel("Skip provider setup and start using Polishly in Demo mode")

                if appState.onboardingState == .providerConnected {
                    Button("Continue") {
                        appState.onboardingState = .ready
                    }
                    .buttonStyle(.borderedProminent)
                    .controlSize(.large)
                    .accessibilityLabel("Continue and start using Polishly with \(appState.selectedProvider.displayName)")
                }
            }
        }
    }

    private var providerSetupCard: some View {
        VStack(alignment: .leading, spacing: 12) {
            Picker("Provider", selection: $appState.selectedProvider) {
                ForEach(LLMProvider.allCases.filter { $0 != .demo }) { provider in
                    Text(provider.displayName).tag(provider)
                }
            }
            .labelsHidden()
            .accessibilityLabel("Rewrite provider")

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
                    // Never expose the key value through accessibility.
                    .accessibilityLabel("\(appState.selectedProvider.displayName) API key")
                    .accessibilityHint("Secure field. Value is not spoken.")
            }

            Button(appState.isTestingProviderConnection ? "Testing…" : "Save & Test Connection") {
                appState.saveAPIKey()
            }
            .keyboardShortcut(.defaultAction)
            .disabled(
                appState.rewriteAPIKey.isEmpty
                || appState.providerConfigurationError != nil
                || appState.isTestingProviderConnection
            )
            .accessibilityLabel("Save and test the \(appState.selectedProvider.displayName) connection")

            if !appState.providerStatusMessage.isEmpty {
                Label(appState.providerStatusMessage, systemImage: appState.providerConnectionState.systemImage)
                    .font(.caption)
                    .foregroundStyle(appState.providerConnectionState.color)
                    .accessibilityLabel(appState.providerStatusMessage)
            }

            Text(appState.selectedProvider.privacyDescription)
                .font(.caption)
                .foregroundStyle(.secondary)
        }
        .padding(18)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: 14))
    }
}

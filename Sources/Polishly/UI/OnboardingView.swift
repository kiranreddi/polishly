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
        .frame(width: 520, height: 470)
        .onAppear { appState.checkAccessibility() }
    }

    private var accessibilityStep: some View {
        VStack(spacing: 26) {
            permissionCard

            VStack(spacing: 8) {
                Button("Continue") {
                    withAnimation {
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
        VStack(spacing: 26) {
            VStack(alignment: .leading, spacing: 14) {
                Text("Provider Setup")
                    .font(.headline)
                    .accessibilityAddTraits(.isHeader)
                Text("Demo rewrites stay on your Mac. Open Settings later to connect OpenAI, Groq, Cerebras, or Anthropic.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.leading)
            }
            .padding(18)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: 14))
            .accessibilityElement(children: .combine)

            VStack(spacing: 8) {
                Button("Start Using Polishly") {
                    appState.onboardingState = .ready
                }
                .buttonStyle(.borderedProminent)
                .controlSize(.large)
                .keyboardShortcut(.defaultAction)
                .accessibilityLabel("Complete onboarding and start using Polishly in Demo mode")

                Text("You can configure AI providers anytime from the menu bar.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.center)
            }
        }
    }
}

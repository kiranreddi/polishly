import SwiftUI

struct OnboardingView: View {
    @ObservedObject var appState = AppState.shared

    var body: some View {
        VStack(spacing: 26) {
            Image(systemName: "text.badge.checkmark")
                .font(.system(size: 42, weight: .medium))
                .foregroundStyle(.tint)

            VStack(spacing: 8) {
                Text("Welcome to Polishly")
                    .font(.largeTitle.weight(.bold))
                Text("Select text in Notes or Teams, press Control–Option–Space, and choose a clearer rewrite.")
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.center)
            }

            permissionCard

            VStack(spacing: 8) {
                Button(appState.isAccessibilityTrusted ? "Start Using Polishly" : "Continue in Demo Mode") {
                    // First launches already default to demo. Onboarding can
                    // reappear when Accessibility trust is lost, so it must not
                    // reset a provider the user configured earlier.
                    appState.showOnboarding = false
                }
                .buttonStyle(.borderedProminent)
                .controlSize(.large)

                Text("Demo rewrites stay on your Mac. Open Settings later to connect OpenAI, Groq, Cerebras, or Anthropic.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.center)
            }
        }
        .padding(40)
        .frame(width: 520, height: 470)
        .onAppear { appState.checkAccessibility() }
    }

    private var permissionCard: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack(spacing: 10) {
                Image(systemName: appState.isAccessibilityTrusted ? "checkmark.circle.fill" : "hand.raised.fill")
                    .foregroundStyle(appState.isAccessibilityTrusted ? .green : .orange)
                VStack(alignment: .leading, spacing: 2) {
                    Text("Accessibility Access")
                        .font(.headline)
                    Text(appState.isAccessibilityTrusted ? "Granted — Polishly is ready." : "Required to read and replace only the text you select.")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                Spacer()
            }

            if !appState.isAccessibilityTrusted {
                HStack {
                    Button("Open System Settings") {
                        appState.openAccessibilitySettings()
                    }
                    .buttonStyle(.borderedProminent)
                    Button("Refresh Status") {
                        appState.checkAccessibility()
                    }
                }
                Text("The status updates automatically after permission is granted.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
        }
        .padding(18)
        .background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: 14))
    }
}

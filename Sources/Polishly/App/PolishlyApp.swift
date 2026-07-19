import SwiftUI
import AppKit

@main
struct PolishlyApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) var appDelegate
    @StateObject private var appState = AppState.shared

    var body: some Scene {
        MenuBarExtra("Polishly", systemImage: "sparkles") {
            Toggle("Pause Polishly", isOn: $appState.isPaused)
            Divider()
            Button("Settings...") {
                SettingsWindowController.shared.show()
            }
            .keyboardShortcut(",", modifiers: .command)

            Divider()

            Button("Quit Polishly") {
                NSApplication.shared.terminate(nil)
            }
            .keyboardShortcut("q", modifiers: .command)
        }

        Window("Onboarding", id: "onboarding") {
            OnboardingView()
                .onReceive(appState.$onboardingState) { state in
                    if state == .ready {
                        NSApp.windows.first(where: { $0.title == "Onboarding" })?.close()
                    }
                }
        }
        .handlesExternalEvents(matching: Set(arrayLiteral: "onboarding"))
        .windowResizability(.contentSize)
        .windowStyle(.hiddenTitleBar)

    }
}

/// AppKit owns one reusable settings window. This avoids duplicate SwiftUI
/// window-scene instances and guarantees that opening Polishly from Applications
/// presents visible UI instead of appearing to do nothing.
final class SettingsWindowController: NSWindowController {
    static let shared = SettingsWindowController()

    private init() {
        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 500, height: 700),
            styleMask: [.titled, .closable, .miniaturizable],
            backing: .buffered,
            defer: false
        )
        window.title = "Settings"
        window.isReleasedWhenClosed = false
        window.contentView = NSHostingView(rootView: SettingsView())
        window.center()
        super.init(window: window)
    }

    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    func show() {
        showWindow(nil)
        window?.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }
}

@MainActor
class AppDelegate: NSObject, NSApplicationDelegate {
    func applicationDidFinishLaunching(_ notification: Notification) {
        // Hide dock icon entirely (already set in Info.plist, but ensure it behaves as UIElement)
        NSApp.setActivationPolicy(.accessory)

        // Passive check to update state quietly
        AppState.shared.checkAccessibility(requestPrompt: false)

        // ShortcutManager will now register independently
        ShortcutManager.shared.start { [weak self] in
            self?.handleRewriteShortcut()
        }

        // SelectionObserver manages itself based on trust/settings state unconditionally
        SelectionObserver.shared.start()

        switch AppDelegate.launchDecision(showOnboarding: AppState.shared.showOnboarding) {
        case .showOnboarding:
            NSApp.activate(ignoringOtherApps: true)
        case .startSilently:
            // Close the Onboarding window if SwiftUI opened it by default
            NSApp.windows.first(where: { $0.title == "Onboarding" })?.close()
        }
    }

    enum LaunchDecision: Equatable {
        case showOnboarding
        case startSilently
    }

    // A pure function of its argument — no reason to require the main actor
    // to call it, and forcing that would needlessly make every caller (and
    // every test of it) async.
    nonisolated static func launchDecision(showOnboarding: Bool) -> LaunchDecision {
        return showOnboarding ? .showOnboarding : .startSilently
    }

    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        SettingsWindowController.shared.show()
        return true
    }

    func applicationWillTerminate(_ notification: Notification) {
        ShortcutManager.shared.stop()
        SelectionObserver.shared.stop()
    }

    func handleRewriteShortcut() {
        guard AppState.shared.isEnabled(for: NSWorkspace.shared.frontmostApplication?.bundleIdentifier) else { return }
        if let capture = SelectionEngine.shared.capture() {
            PopupController.shared.show(for: capture)
        } else {
            print("Failed to capture text")
        }
    }
}

import SwiftUI
import Combine

@main
struct PolishlyApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) var appDelegate
    @StateObject private var appState = AppState.shared
    @Environment(\.openWindow) var openWindow
    
    var body: some Scene {
        MenuBarExtra("Polishly", systemImage: "sparkles") {
            Toggle("Pause Polishly", isOn: $appState.isPaused)
            Divider()
            Button("Settings...") {
                openWindow(id: "settings")
                NSApp.activate(ignoringOtherApps: true)
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
                .onReceive(appState.$showOnboarding) { show in
                    if !show {
                        NSApp.windows.first(where: { $0.title == "Onboarding" })?.close()
                    }
                }
        }
        .handlesExternalEvents(matching: Set(arrayLiteral: "onboarding"))
        .windowResizability(.contentSize)
        .windowStyle(.hiddenTitleBar)
        
        Window("Settings", id: "settings") {
            SettingsView()
        }
        .handlesExternalEvents(matching: Set(arrayLiteral: "settings"))
        .windowResizability(.contentSize)
    }
}

class AppDelegate: NSObject, NSApplicationDelegate {
    private var shortcutPermissionSubscription: AnyCancellable?

    func applicationDidFinishLaunching(_ notification: Notification) {
        // Hide dock icon entirely (already set in Info.plist, but ensure it behaves as UIElement)
        NSApp.setActivationPolicy(.accessory)

        // An event tap cannot be created before Accessibility is granted. Keep
        // the shortcut listener synchronized with live permission changes so a
        // user does not have to discover that Polishly needs to be relaunched.
        shortcutPermissionSubscription = AppState.shared.$isAccessibilityTrusted
            .removeDuplicates()
            .receive(on: RunLoop.main)
            .sink { [weak self] isTrusted in
                if isTrusted {
                    ShortcutManager.shared.start { [weak self] in
                        self?.handleRewriteShortcut()
                    }
                } else {
                    ShortcutManager.shared.stop()
                }
            }
        
        // WindowGroup creates the onboarding window locally. Do not open the
        // app's URL scheme here: a stale development copy with the same scheme
        // could otherwise be launched instead of this running build.
        if AppState.shared.showOnboarding {
            NSApp.activate(ignoringOtherApps: true)
        }
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

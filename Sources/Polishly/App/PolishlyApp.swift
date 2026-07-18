import SwiftUI

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
        
        WindowGroup("Onboarding", id: "onboarding") {
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
        
        WindowGroup("Settings", id: "settings") {
            SettingsView()
        }
        .handlesExternalEvents(matching: Set(arrayLiteral: "settings"))
        .windowResizability(.contentSize)
    }
}

class AppDelegate: NSObject, NSApplicationDelegate {
    func applicationDidFinishLaunching(_ notification: Notification) {
        // Hide dock icon entirely (already set in Info.plist, but ensure it behaves as UIElement)
        NSApp.setActivationPolicy(.accessory)
        
        ShortcutManager.shared.start { [weak self] in
            self?.handleRewriteShortcut()
        }
        
        if AppState.shared.showOnboarding {
            if let url = URL(string: "polishly://onboarding") {
                NSWorkspace.shared.open(url)
            }
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

import SwiftUI
import AppKit

struct DiscoveryTriggerView: View {
    let action: () -> Void
    @State private var isHovered = false

    var body: some View {
        Button(action: action) {
            HStack(spacing: 4) {
                Image(systemName: "sparkles")
                Text("Rewrite")
            }
            .font(.system(size: 12, weight: .medium))
            .foregroundColor(isHovered ? .white : Color(hex: "008c80"))
            .padding(.horizontal, 8)
            .padding(.vertical, 4)
            .background(isHovered ? Color(hex: "008c80") : .white)
            .cornerRadius(6)
            .shadow(color: Color.black.opacity(0.1), radius: 3, y: 1)
            .overlay(
                RoundedRectangle(cornerRadius: 6)
                    .stroke(Color(hex: "008c80").opacity(0.3), lineWidth: 1)
            )
        }
        .buttonStyle(.plain)
        .onHover { hovered in
            isHovered = hovered
        }
        .frame(width: DiscoveryTriggerController.triggerSize.width,
               height: DiscoveryTriggerController.triggerSize.height)
    }
}

class DiscoveryTriggerController {
    static let shared = DiscoveryTriggerController()
    static let triggerSize = NSSize(width: 90, height: 30)

    private var window: NSWindow?
    private var currentCapture: SelectionEngine.CapturedText?
    private var isAXCapture: Bool = false
    private var outsideClickMonitor: Any?
    private var escapeMonitor: Any?

    private init() {}

    func show(at point: CGPoint, capture: SelectionEngine.CapturedText?, isAX: Bool) {
        self.currentCapture = capture
        self.isAXCapture = isAX

        if window == nil {
            let size = Self.triggerSize
            let win = NSPanel(
                contentRect: NSRect(x: 0, y: 0, width: size.width, height: size.height),
                styleMask: [.borderless, .nonactivatingPanel],
                backing: .buffered,
                defer: false
            )
            win.isOpaque = false
            win.backgroundColor = .clear
            win.level = .floating
            win.hasShadow = false
            win.collectionBehavior = [.canJoinAllSpaces, .transient, .ignoresCycle]

            let host = NSHostingView(rootView: DiscoveryTriggerView(action: { [weak self] in
                self?.triggerRewrite()
            }))
            host.sizingOptions = []
            host.frame = NSRect(x: 0, y: 0, width: size.width, height: size.height)
            host.autoresizingMask = [.width, .height]
            win.contentView = host
            win.setContentSize(size)
            self.window = win
        }

        let appKitPoint = isAX ? ScreenCoordinates.appKitPoint(fromAccessibility: point) : point
        let targetScreen = ScreenCoordinates.screenContaining(appKitPoint)
        let size = Self.triggerSize

        // Point is the top-left of the trigger in AppKit space after conversion.
        var origin = NSPoint(x: appKitPoint.x, y: appKitPoint.y - size.height)
        origin = ScreenCoordinates.clampOrigin(origin, size: size, in: targetScreen.visibleFrame)

        window?.setFrame(NSRect(origin: origin, size: size), display: true)
        window?.orderFrontRegardless()
        installDismissMonitors()
    }

    func hide() {
        removeDismissMonitors()
        window?.orderOut(nil)
        currentCapture = nil
    }

    var isVisible: Bool {
        window?.isVisible == true
    }

    private func triggerRewrite() {
        let localCapture = currentCapture
        let localIsAX = isAXCapture
        let bundleId = localCapture?.sourceBundleIdentifier
            ?? NSWorkspace.shared.frontmostApplication?.bundleIdentifier
        let preferClipboard = AppCapabilityManager.shared.prefersClipboardInteraction(for: bundleId)
        hide()

        // Teams/Electron: never trust a cached AX capture for the rewrite —
        // re-capture via Cmd+C so Accept can paste-replace reliably.
        if localIsAX, let axCapture = localCapture, !preferClipboard {
            PopupController.shared.show(for: axCapture)
        } else if let newCapture = SelectionEngine.shared.capture(forceClipboard: true) {
            PopupController.shared.show(for: newCapture)
        }
    }

    private func installDismissMonitors() {
        removeDismissMonitors()
        outsideClickMonitor = NSEvent.addGlobalMonitorForEvents(matching: .leftMouseDown) { [weak self] event in
            guard let self, let window = self.window else { return }
            if !window.frame.contains(NSEvent.mouseLocation) { self.hide() }
        }
        escapeMonitor = NSEvent.addGlobalMonitorForEvents(matching: .keyDown) { [weak self] event in
            if event.keyCode == 53 { self?.hide() }
        }
    }

    private func removeDismissMonitors() {
        if let outsideClickMonitor { NSEvent.removeMonitor(outsideClickMonitor); self.outsideClickMonitor = nil }
        if let escapeMonitor { NSEvent.removeMonitor(escapeMonitor); self.escapeMonitor = nil }
    }

    // MARK: - Test Hooks
    var test_window: NSWindow? { window }
}

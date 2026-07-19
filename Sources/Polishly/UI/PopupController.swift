import Cocoa
import SwiftUI
import Combine

/// A borderless panel (no `.titled`/`.closable`/`.resizable`) defaults
/// `canBecomeKeyWindow` to NO — `.nonactivatingPanel` only controls whether
/// becoming key also activates the owning app, it doesn't grant key-ability
/// itself. Without this override, `makeKey()` fails silently and any text
/// field in the panel never receives typed keystrokes.
final class KeyableNonactivatingPanel: NSPanel {
    override var canBecomeKey: Bool { true }
}

/// Shared Accessibility (CoreGraphics, top-left) ↔ AppKit (bottom-left) helpers.
enum ScreenCoordinates {
    /// Flip a CG/AX rect into AppKit global coordinates using the main display height.
    static func appKitRect(fromAccessibility rect: CGRect) -> NSRect {
        let mainScreenHeight = CGDisplayBounds(CGMainDisplayID()).height
        return NSRect(
            x: rect.origin.x,
            y: mainScreenHeight - rect.maxY,
            width: rect.width,
            height: rect.height
        )
    }

    /// Flip a CG/AX point into AppKit global coordinates.
    static func appKitPoint(fromAccessibility point: CGPoint) -> NSPoint {
        let mainScreenHeight = CGDisplayBounds(CGMainDisplayID()).height
        return NSPoint(x: point.x, y: mainScreenHeight - point.y)
    }

    static func screenContaining(_ point: NSPoint, fallbackRect: NSRect? = nil) -> NSScreen {
        // NSRect.contains treats maxX/maxY as exclusive — nudge edge points inward.
        if let match = NSScreen.screens.first(where: { $0.frame.insetBy(dx: -1, dy: -1).contains(point) }) {
            return match
        }
        if let fallbackRect,
           let match = NSScreen.screens.first(where: { $0.frame.intersects(fallbackRect) }) {
            return match
        }
        // Nearest screen by squared distance to frame (multi-monitor edges).
        let nearest = NSScreen.screens.min { a, b in
            distanceSquared(point, to: a.frame) < distanceSquared(point, to: b.frame)
        }
        return nearest ?? NSScreen.main ?? NSScreen.screens.first!
    }

    private static func distanceSquared(_ point: NSPoint, to rect: NSRect) -> CGFloat {
        let dx = max(rect.minX - point.x, 0, point.x - rect.maxX)
        let dy = max(rect.minY - point.y, 0, point.y - rect.maxY)
        return dx * dx + dy * dy
    }

    /// Clamp a window origin so `size` stays inside `visibleFrame`.
    static func clampOrigin(
        _ origin: NSPoint,
        size: NSSize,
        in visibleFrame: NSRect,
        padding: CGFloat = 12
    ) -> NSPoint {
        var x = origin.x
        var y = origin.y
        let minX = visibleFrame.minX + padding
        let maxX = visibleFrame.maxX - size.width - padding
        let minY = visibleFrame.minY + padding
        let maxY = visibleFrame.maxY - size.height - padding
        x = max(minX, min(x, max(minX, maxX)))
        y = max(minY, min(y, max(minY, maxY)))
        return NSPoint(x: x, y: y)
    }
}

class PopupController: NSWindowController, NSWindowDelegate {
    static let shared = PopupController()

    static let cardSize = NSSize(width: 430, height: 300)

    let viewModel = PopupViewModel()
    private var capturedText: SelectionEngine.CapturedText?
    private var reviseInputSubscription: AnyCancellable?
    private var outsideClickMonitor: Any?
    private var escapeMonitor: Any?

    private init() {
        let panel = KeyableNonactivatingPanel(
            contentRect: NSRect(x: 0, y: 0, width: Self.cardSize.width, height: Self.cardSize.height),
            styleMask: [.nonactivatingPanel, .fullSizeContentView],
            backing: .buffered,
            defer: false
        )

        panel.isFloatingPanel = true
        panel.level = .floating
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        panel.hasShadow = false
        panel.backgroundColor = .clear
        panel.isOpaque = false

        super.init(window: panel)

        panel.delegate = self

        let view = PopupCardView(viewModel: viewModel)
        let hostingView = NSHostingView(rootView: view)
        // Empty sizing options — never let SwiftUI drive AppKit frame feedback loops.
        hostingView.sizingOptions = []
        panel.contentView = hostingView
        panel.setContentSize(Self.cardSize)

        viewModel.closeAction = { [weak self] in
            self?.closePanel()
        }

        // Promote to key only when the revise field needs keystrokes.
        reviseInputSubscription = viewModel.$showReviseInput
            .receive(on: DispatchQueue.main)
            .sink { [weak self] showReviseInput in
                guard showReviseInput, let window = self?.window else { return }
                window.makeKey()
            }
    }

    required init?(coder: NSCoder) {
        fatalError("init(coder:) not implemented")
    }

    func show(for capture: SelectionEngine.CapturedText) {
        capturedText = capture
        viewModel.configure(with: capture)

        if let bounds = capture.bounds {
            position(relativeTo: bounds)
        } else if let primaryScreen = NSScreen.screens.first {
            // Electron apps often omit AX bounds — anchor near the mouse.
            let mouse = NSEvent.mouseLocation
            let coreGraphicsAnchor = NSRect(
                x: mouse.x,
                y: primaryScreen.frame.maxY - mouse.y,
                width: 0,
                height: 0
            )
            position(relativeTo: coreGraphicsAnchor)
        }

        // Nonactivating panel must be ordered above the source app explicitly.
        window?.orderFrontRegardless()
        installDismissMonitors()
    }

    func showPreview() {
        let preview = SelectionEngine.CapturedText(
            text: "i have sent the mail let see what he will. i gave a branch to Justin to run the shim and firmware.",
            method: .accessibility,
            bounds: nil,
            axElement: nil,
            sourceBundleIdentifier: nil
        )
        show(for: preview)
    }

    private func position(relativeTo rect: CGRect) {
        guard let window = self.window else { return }

        let cardSize = Self.cardSize
        var frame = NSRect(x: 0, y: 0, width: cardSize.width, height: cardSize.height)

        let appKitRect = ScreenCoordinates.appKitRect(fromAccessibility: rect)
        let selectionPoint = NSPoint(x: appKitRect.midX, y: appKitRect.midY)
        let targetScreen = ScreenCoordinates.screenContaining(selectionPoint, fallbackRect: appKitRect)
        let visibleFrame = targetScreen.visibleFrame

        // Prefer above-left of the selection; flip below if it would leave the display.
        var origin = NSPoint(
            x: appKitRect.minX - 20,
            y: appKitRect.maxY + 14
        )
        if origin.y + cardSize.height > visibleFrame.maxY - 12 {
            origin.y = appKitRect.minY - cardSize.height - 14
        }

        origin = ScreenCoordinates.clampOrigin(origin, size: cardSize, in: visibleFrame)
        frame.origin = origin
        window.setFrame(frame, display: true)
    }

    func closePanel() {
        removeDismissMonitors()
        window?.orderOut(nil)
    }

    private func installDismissMonitors() {
        removeDismissMonitors()
        outsideClickMonitor = NSEvent.addGlobalMonitorForEvents(matching: .leftMouseDown) { [weak self] event in
            guard let self, let window = self.window else { return }
            if !window.frame.contains(NSEvent.mouseLocation) { self.closePanel() }
        }
        // Global Escape — local monitors miss keyDown while Polishly is accessory/non-key.
        escapeMonitor = NSEvent.addGlobalMonitorForEvents(matching: .keyDown) { [weak self] event in
            if event.keyCode == 53 { self?.closePanel() }
        }
    }

    private func removeDismissMonitors() {
        if let outsideClickMonitor { NSEvent.removeMonitor(outsideClickMonitor); self.outsideClickMonitor = nil }
        if let escapeMonitor { NSEvent.removeMonitor(escapeMonitor); self.escapeMonitor = nil }
    }
}

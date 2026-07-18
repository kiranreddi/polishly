import Cocoa
import SwiftUI
import Combine

class PopupController: NSWindowController, NSWindowDelegate {
    static let shared = PopupController()
    
    let viewModel = PopupViewModel()
    private var capturedText: SelectionEngine.CapturedText?
    private var updateSubscription: AnyCancellable?
    private var outsideClickMonitor: Any?
    private var escapeMonitor: Any?
    
    private init() {
        let panel = NSPanel(
            contentRect: NSRect(x: 0, y: 0, width: 430, height: 200),
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
        hostingView.sizingOptions = []
        panel.contentView = hostingView
        
        viewModel.closeAction = { [weak self] in
            self?.closePanel()
        }
        updateSubscription = viewModel.objectWillChange.sink { [weak self] in
            guard let self, let capturedText = self.capturedText, let bounds = capturedText.bounds else { return }
            DispatchQueue.main.async { self.position(relativeTo: bounds) }
        }
    }
    
    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }
    
    func show(for capture: SelectionEngine.CapturedText) {
        capturedText = capture
        viewModel.configure(with: capture)
        
        if let bounds = capture.bounds {
            position(relativeTo: bounds)
        } else {
            // Electron apps do not always expose a selection range. The mouse
            // remains next to a drag-selection, so use it as the anchor while
            // preserving the CoreGraphics coordinate convention below.
            if let primaryScreen = NSScreen.screens.first {
                let mouse = NSEvent.mouseLocation
                let coreGraphicsAnchor = NSRect(
                    x: mouse.x,
                    y: primaryScreen.frame.maxY - mouse.y,
                    width: 0,
                    height: 0
                )
                position(relativeTo: coreGraphicsAnchor)
            }
        }
        
        // A nonactivating panel must be explicitly ordered above the source app.
        // `makeKeyAndOrderFront` is insufficient while Polishly is an accessory app.
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
        guard let window = self.window, let primaryScreen = NSScreen.screens.first else { return }
        
        // Size to fit the SwiftUI content
        if let view = window.contentView as? NSHostingView<PopupCardView> {
            let size = view.fittingSize
            // A freshly-created hosting view can report a transient zero fitting
            // height before its first layout pass. Preserve a usable card frame.
            let cardSize = NSSize(width: max(430, size.width), height: max(240, size.height))
            var frame = NSRect(x: 0, y: 0, width: cardSize.width, height: cardSize.height)
            
            // Accessibility bounds use the global CoreGraphics coordinate space
            // (origin at the primary display's top-left). AppKit uses a global
            // bottom-left origin. Convert first, then choose the display that
            // actually contains the selection. `NSScreen.main` can still be the
            // Settings display while the user is writing on another monitor.
            let appKitRect = NSRect(
                x: rect.origin.x,
                y: primaryScreen.frame.maxY - rect.maxY,
                width: rect.width,
                height: rect.height
            )
            let selectionPoint = NSPoint(x: appKitRect.midX, y: appKitRect.midY)
            let targetScreen = NSScreen.screens.first(where: { $0.frame.contains(selectionPoint) })
                ?? NSScreen.screens.first(where: { $0.frame.intersects(appKitRect) })
                ?? NSScreen.main
                ?? primaryScreen
            let visibleFrame = targetScreen.visibleFrame
            
            // Anchor above
            frame.origin.x = max(
                visibleFrame.minX + 12,
                min(appKitRect.minX - 20, visibleFrame.maxX - cardSize.width - 16)
            )
            var top = appKitRect.maxY + 14
            
            // If it goes off top of screen, flip below
            if top + cardSize.height > visibleFrame.maxY - 12 {
                top = appKitRect.minY - cardSize.height - 14
            }

            // A very tall card or a selection near a screen edge must remain on
            // the same display instead of leaking onto an adjacent monitor.
            frame.origin.y = max(
                visibleFrame.minY + 12,
                min(top, visibleFrame.maxY - cardSize.height - 12)
            )
            window.setFrame(frame, display: true)
        }
    }
    
    func closePanel() {
        removeDismissMonitors()
        window?.orderOut(nil)
    }

    private func installDismissMonitors() {
        removeDismissMonitors()
        outsideClickMonitor = NSEvent.addGlobalMonitorForEvents(matching: .leftMouseDown) { [weak self] event in
            guard let self, let window = self.window else { return }
            // A global monitor's location is not in the panel's coordinate space.
            // Use the current screen position so any outside click closes reliably.
            if !window.frame.contains(NSEvent.mouseLocation) { self.closePanel() }
        }
        escapeMonitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { [weak self] event in
            if event.keyCode == 53 { self?.closePanel(); return nil }
            return event
        }
    }

    private func removeDismissMonitors() {
        if let outsideClickMonitor { NSEvent.removeMonitor(outsideClickMonitor); self.outsideClickMonitor = nil }
        if let escapeMonitor { NSEvent.removeMonitor(escapeMonitor); self.escapeMonitor = nil }
    }
}

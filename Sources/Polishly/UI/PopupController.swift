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
            // Fallback position to mouse or center screen
            if let screen = NSScreen.main {
                let rect = NSRect(x: screen.frame.midX - 215, y: screen.frame.midY, width: 0, height: 0)
                position(relativeTo: rect)
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
        guard let window = self.window, let screen = NSScreen.main else { return }
        
        // Size to fit the SwiftUI content
        if let view = window.contentView as? NSHostingView<PopupCardView> {
            let size = view.fittingSize
            // A freshly-created hosting view can report a transient zero fitting
            // height before its first layout pass. Preserve a usable card frame.
            let cardSize = NSSize(width: max(430, size.width), height: max(240, size.height))
            var frame = NSRect(x: 0, y: 0, width: cardSize.width, height: cardSize.height)
            
            // CoreGraphics rect (origin top-left) vs AppKit rect (origin bottom-left)
            // rect here is typically in CG coords if derived from AX.
            // The CG→AppKit flip must use the primary screen's height —
            // NSScreen.main is merely the screen with keyboard focus.
            let primaryHeight = NSScreen.screens.first?.frame.height ?? screen.frame.height
            let appKitRect = NSRect(x: rect.origin.x, y: primaryHeight - rect.origin.y - rect.height, width: rect.width, height: rect.height)
            
            // Anchor above
            frame.origin.x = max(12, min(appKitRect.minX - 20, screen.frame.maxX - cardSize.width - 16))
            var top = appKitRect.maxY + 14
            
            // If it goes off top of screen, flip below
            if top + cardSize.height > screen.frame.maxY - 46 {
                top = appKitRect.minY - cardSize.height - 14
            }
            
            frame.origin.y = top
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

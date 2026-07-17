import Cocoa
import SwiftUI

class PopupController: NSWindowController, NSWindowDelegate {
    static let shared = PopupController()
    
    let viewModel = PopupViewModel()
    
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
    }
    
    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }
    
    func show(for capture: SelectionEngine.CapturedText) {
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
        
        window?.makeKeyAndOrderFront(nil)
    }
    
    private func position(relativeTo rect: CGRect) {
        guard let window = self.window, let screen = NSScreen.main else { return }
        
        // Size to fit the SwiftUI content
        if let view = window.contentView as? NSHostingView<PopupCardView> {
            let size = view.fittingSize
            var frame = NSRect(x: 0, y: 0, width: size.width, height: size.height)
            
            // CoreGraphics rect (origin top-left) vs AppKit rect (origin bottom-left)
            // rect here is typically in CG coords if derived from AX
            let screenHeight = screen.frame.height
            let appKitRect = NSRect(x: rect.origin.x, y: screenHeight - rect.origin.y - rect.height, width: rect.width, height: rect.height)
            
            // Anchor above
            frame.origin.x = max(12, min(appKitRect.minX - 20, screen.frame.maxX - size.width - 16))
            var top = appKitRect.maxY + 14
            
            // If it goes off top of screen, flip below
            if top + size.height > screen.frame.maxY - 46 {
                top = appKitRect.minY - size.height - 14
            }
            
            frame.origin.y = top
            window.setFrame(frame, display: true)
        }
    }
    
    func closePanel() {
        window?.orderOut(nil)
    }
    
    // Auto-close on deactivate if needed, or close on ESC (handled by SwiftUI or global monitor)
}

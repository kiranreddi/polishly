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
    }
}

class DiscoveryTriggerController {
    static let shared = DiscoveryTriggerController()
    
    private var window: NSWindow?
    private var currentCapture: SelectionEngine.CapturedText?
    private var isAXCapture: Bool = false
    
    private init() {}
    
    func show(at point: CGPoint, capture: SelectionEngine.CapturedText?, isAX: Bool) {
        self.currentCapture = capture
        self.isAXCapture = isAX
        
        if window == nil {
            let win = NSPanel(
                contentRect: NSRect(x: 0, y: 0, width: 90, height: 30),
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
            host.frame = NSRect(x: 0, y: 0, width: 90, height: 30)
            host.autoresizingMask = [.width, .height]
            win.contentView = host
            self.window = win
        }
        
        // Convert CoreGraphics coordinates (top-left) to AppKit (bottom-left) if needed
        var appKitPoint = point
        if isAX {
            // CoreGraphics uses top-left of the primary display (screen 0)
            let mainScreenHeight = NSScreen.screens.first?.frame.height ?? 0
            appKitPoint.y = mainScreenHeight - point.y
        }
        
        // Use monitor-aware coordinate clamping
        var targetScreen = NSScreen.main
        for screen in NSScreen.screens {
            if screen.frame.contains(appKitPoint) {
                targetScreen = screen
                break
            }
        }
        
        let screenFrame = targetScreen?.visibleFrame ?? NSScreen.main?.visibleFrame ?? .zero
        
        var clampedX = appKitPoint.x
        var clampedY = appKitPoint.y
        
        // Ensure it doesn't go off-screen
        let winWidth: CGFloat = 90
        let winHeight: CGFloat = 30
        if clampedX + winWidth > screenFrame.maxX {
            clampedX = screenFrame.maxX - winWidth
        }
        if clampedY - winHeight < screenFrame.minY {
            clampedY = screenFrame.minY + winHeight
        }
        
        window?.setFrameOrigin(NSPoint(x: clampedX, y: clampedY - winHeight))
        window?.orderFrontRegardless() // Does not steal focus
    }
    
    func hide() {
        window?.orderOut(nil)
        currentCapture = nil
    }
    
    var isVisible: Bool {
        return window?.isVisible == true
    }
    
    private func triggerRewrite() {
        // Capture local values first before hiding, as hide() clears currentCapture
        let localCapture = currentCapture
        let localIsAX = isAXCapture
        hide() // Now safe to hide
        
        if localIsAX, let axCapture = localCapture {
            // Keep AX captures on the AX path
            PopupController.shared.show(for: axCapture)
        } else {
            // Fallback path or non-AX, force clipboard capture
            if let newCapture = SelectionEngine.shared.capture(forceClipboard: true) {
                PopupController.shared.show(for: newCapture)
            } else {
                print("Failed to capture text via clipboard fallback")
            }
        }
    }
    
    // MARK: - Test Hooks
    var test_window: NSWindow? { window }
}

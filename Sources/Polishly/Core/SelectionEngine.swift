import Foundation
import Cocoa

class SelectionEngine {
    static let shared = SelectionEngine()
    
    struct CapturedText {
        let text: String
        let method: CaptureMethod
        let bounds: CGRect?
        let axElement: AXUIElement?
        let sourceBundleIdentifier: String?
    }
    
    enum CaptureMethod {
        case accessibility
        case clipboard
    }
    
    private init() {}
    
    func capture() -> CapturedText? {
        // Try Tier A: Accessibility
        if let element = AccessibilityManager.shared.getFocusedElement(),
           let text = AccessibilityManager.shared.getSelectedText(from: element),
           !text.isEmpty {
            
            let bounds = AccessibilityManager.shared.getSelectionBounds(from: element)
            return CapturedText(text: text, method: .accessibility, bounds: bounds, axElement: element, sourceBundleIdentifier: NSWorkspace.shared.frontmostApplication?.bundleIdentifier)
        }
        
        // Try Tier B: Clipboard Fallback
        let snapshot = ClipboardManager.shared.takeSnapshot()
        
        // We synthesize Cmd+C
        ClipboardManager.shared.synthesizeCopy()
        
        // Wait briefly for the clipboard to populate
        Thread.sleep(forTimeInterval: 0.15)
        
        // Read what was copied
        if let text = ClipboardManager.shared.readString(), !text.isEmpty {
            // Restore clipboard immediately to not pollute user's history
            let copyChangeCount = NSPasteboard.general.changeCount
            _ = ClipboardManager.shared.restore(snapshot: snapshot, expectedChangeCount: copyChangeCount)
            return CapturedText(text: text, method: .clipboard, bounds: nil, axElement: nil, sourceBundleIdentifier: NSWorkspace.shared.frontmostApplication?.bundleIdentifier)
        }
        
        // If we copied nothing, restore anyway
        _ = ClipboardManager.shared.restore(snapshot: snapshot, expectedChangeCount: NSPasteboard.general.changeCount)
        
        return nil
    }
    
    func inject(text: String, originalCapture: CapturedText, completion: @escaping (Bool) -> Void) {
        if originalCapture.method == .accessibility, let element = originalCapture.axElement {
            let success = AccessibilityManager.shared.replaceSelectedText(in: element, with: text)
            if success {
                completion(true)
                return
            }
        }
        
        // Fallback to Clipboard Paste
        // Never synthesize paste into a different app than the one that owned the selection.
        guard NSWorkspace.shared.frontmostApplication?.bundleIdentifier == originalCapture.sourceBundleIdentifier else {
            completion(false)
            return
        }

        let snapshot = ClipboardManager.shared.takeSnapshot()
        let polishlyWriteChangeCount = ClipboardManager.shared.writeString(text)
        
        ClipboardManager.shared.synthesizePaste()
        
        // We need to wait for the paste to complete before restoring the clipboard.
        // If we restore too fast, the app pastes the restored content instead.
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.3) {
            _ = ClipboardManager.shared.restore(snapshot: snapshot, expectedChangeCount: polishlyWriteChangeCount)
            completion(true)
        }
    }
}

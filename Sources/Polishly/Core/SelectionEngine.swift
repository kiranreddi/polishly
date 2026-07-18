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

    func capture(forceClipboard: Bool = false) -> CapturedText? {
        // Try Tier A: Accessibility
        if !forceClipboard,
           let element = AccessibilityManager.shared.getFocusedElement(),
           let text = AccessibilityManager.shared.getSelectedText(from: element),
           !text.isEmpty {

            let bounds = AccessibilityManager.shared.getSelectionBounds(from: element)
            return CapturedText(text: text, method: .accessibility, bounds: bounds, axElement: element, sourceBundleIdentifier: NSWorkspace.shared.frontmostApplication?.bundleIdentifier)
        }

        // Try Tier B: Clipboard Fallback
        let snapshot = ClipboardManager.shared.takeSnapshot()

        // We synthesize Cmd+C
        _ = ClipboardManager.shared.synthesizeCopy()

        // Wait briefly for the clipboard to populate
        Thread.sleep(forTimeInterval: 0.15)

        // If the pasteboard never changed, the app copied nothing (no selection,
        // a secure field, or missing permission). Bail out rather than treating
        // stale clipboard contents — possibly sensitive — as the selection.
        guard NSPasteboard.general.changeCount != snapshot.changeCount else {
            return nil
        }

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

    enum InjectionResult {
        case success
        case failed
        case unconfirmed
        case pasteSentUnconfirmable
    }

    func inject(text: String, originalCapture: CapturedText, completion: @escaping (InjectionResult) -> Void) {
        if originalCapture.method == .accessibility, let element = originalCapture.axElement {
            let success = AccessibilityManager.shared.replaceSelectedText(in: element, with: text)
            if success {
                completion(.success)
                return
            }
        }

        // Fallback to Clipboard Paste
        guard NSWorkspace.shared.frontmostApplication?.bundleIdentifier == originalCapture.sourceBundleIdentifier else {
            completion(.failed)
            return
        }

        let snapshot = ClipboardManager.shared.takeSnapshot()
        let polishlyWriteChangeCount = ClipboardManager.shared.writeString(text)

        let pasteAttempted = ClipboardManager.shared.synthesizePaste()

        if pasteAttempted {
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) {
                _ = ClipboardManager.shared.restore(snapshot: snapshot, expectedChangeCount: polishlyWriteChangeCount)
            }
            completion(.pasteSentUnconfirmable)
        } else {
            // Paste event couldn't even be created. Tell user to press Cmd-V.
            // Leave the rewritten text on the clipboard: restoring the old
            // contents on a timer would make a slightly-late Cmd-V paste
            // stale — possibly sensitive — content instead of the rewrite.
            completion(.unconfirmed)
        }
    }
}

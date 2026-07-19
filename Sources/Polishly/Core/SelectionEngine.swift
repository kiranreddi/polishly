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
        let bundleId = NSWorkspace.shared.frontmostApplication?.bundleIdentifier
        let preferClipboard = forceClipboard
            || AppCapabilityManager.shared.prefersClipboardInteraction(for: bundleId)

        // Tier A — Accessibility (skipped for Electron hosts like Teams).
        if !preferClipboard,
           let element = AccessibilityManager.shared.getFocusedElement(),
           let text = AccessibilityManager.shared.getSelectedText(from: element),
           !text.isEmpty {

            let bounds = AccessibilityManager.shared.getSelectionBounds(from: element)
            return CapturedText(
                text: text,
                method: .accessibility,
                bounds: bounds,
                axElement: element,
                sourceBundleIdentifier: bundleId
            )
        }

        // Tier B — Clipboard fallback (primary path for Teams).
        let snapshot = ClipboardManager.shared.takeSnapshot()
        _ = ClipboardManager.shared.synthesizeCopy()

        // Electron apps often exceed a fixed 150ms; poll up to ~450ms.
        let copyTimeout: TimeInterval = preferClipboard ? 0.45 : 0.20
        guard ClipboardManager.shared.waitForPasteboardChange(
            from: snapshot.changeCount,
            timeout: copyTimeout
        ) else {
            return nil
        }

        if let text = ClipboardManager.shared.readString(), !text.isEmpty {
            let copyChangeCount = NSPasteboard.general.changeCount
            _ = ClipboardManager.shared.restore(snapshot: snapshot, expectedChangeCount: copyChangeCount)
            return CapturedText(
                text: text,
                method: .clipboard,
                bounds: nil,
                axElement: nil,
                sourceBundleIdentifier: bundleId
            )
        }

        _ = ClipboardManager.shared.restore(
            snapshot: snapshot,
            expectedChangeCount: NSPasteboard.general.changeCount
        )
        return nil
    }

    enum InjectionResult {
        case success
        case failed
        case unconfirmed
        case pasteSentUnconfirmable
    }

    func inject(text: String, originalCapture: CapturedText, completion: @escaping (InjectionResult) -> Void) {
        let bundleId = originalCapture.sourceBundleIdentifier
        let preferClipboard = AppCapabilityManager.shared.prefersClipboardInteraction(for: bundleId)

        // Tier A — AX write only when the host is known-good (Notes, etc.).
        // Teams/Electron often report AX success without replacing the field.
        if !preferClipboard,
           originalCapture.method == .accessibility,
           let element = originalCapture.axElement {
            let success = AccessibilityManager.shared.replaceSelectedText(in: element, with: text)
            if success {
                completion(.success)
                return
            }
        }

        // Ensure Cmd+V lands in the source app's field, not the rewrite card.
        // Electron hosts always need an explicit activate; others only when focus moved.
        if preferClipboard
            || NSWorkspace.shared.frontmostApplication?.bundleIdentifier != bundleId {
            guard activateSourceApp(bundleIdentifier: bundleId) else {
                completion(.failed)
                return
            }
        }

        guard NSWorkspace.shared.frontmostApplication?.bundleIdentifier == bundleId else {
            completion(.failed)
            return
        }

        let snapshot = ClipboardManager.shared.takeSnapshot()
        let polishlyWriteChangeCount = ClipboardManager.shared.writeString(text)

        // Give Electron a beat to observe the new pasteboard contents.
        if preferClipboard {
            Thread.sleep(forTimeInterval: 0.05)
        }

        let pasteAttempted = ClipboardManager.shared.synthesizePaste()

        if pasteAttempted {
            let restoreDelay: TimeInterval = preferClipboard ? 0.7 : 0.5
            DispatchQueue.main.asyncAfter(deadline: .now() + restoreDelay) {
                _ = ClipboardManager.shared.restore(
                    snapshot: snapshot,
                    expectedChangeCount: polishlyWriteChangeCount
                )
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

    @discardableResult
    private func activateSourceApp(bundleIdentifier: String?) -> Bool {
        guard let bundleIdentifier else { return false }
        let app = NSRunningApplication.runningApplications(withBundleIdentifier: bundleIdentifier)
            .first(where: { !$0.isTerminated })
        guard let app else { return false }
        let activated = app.activate()
        if activated || app.isActive {
            // Focus handoff into Electron compose boxes is not instantaneous.
            Thread.sleep(forTimeInterval: 0.08)
            return true
        }
        return false
    }
}

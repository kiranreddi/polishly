import Cocoa
import CoreGraphics

class ClipboardManager {
    static let shared = ClipboardManager()

    struct Snapshot {
        let changeCount: Int
        let items: [NSPasteboardItem]
    }

    private init() {}

    func takeSnapshot() -> Snapshot {
        let pb = NSPasteboard.general
        let count = pb.changeCount
        var items: [NSPasteboardItem] = []

        if let pbItems = pb.pasteboardItems {
            for item in pbItems {
                let newItem = NSPasteboardItem()
                for type in item.types {
                    if let data = item.data(forType: type) {
                        newItem.setData(data, forType: type)
                    }
                }
                items.append(newItem)
            }
        }

        return Snapshot(changeCount: count, items: items)
    }

    /// Restores only when the pasteboard is still in the state Polishly wrote.
    /// Comparing with the pre-transaction count would always fail after a write;
    /// comparing with this token protects a newer user copy operation instead.
    func restore(snapshot: Snapshot, expectedChangeCount: Int) -> Bool {
        let pb = NSPasteboard.general
        if pb.changeCount != expectedChangeCount {
            // The clipboard has changed since the snapshot. Don't clobber it.
            return false
        }

        pb.clearContents()
        pb.writeObjects(snapshot.items)
        return true
    }

    func synthesizeCopy() -> Bool {
        let src = CGEventSource(stateID: .hidSystemState)
        let cmdd = CGEvent(keyboardEventSource: src, virtualKey: 0x08, keyDown: true)
        cmdd?.flags = .maskCommand
        let cmdu = CGEvent(keyboardEventSource: src, virtualKey: 0x08, keyDown: false)
        cmdu?.flags = .maskCommand

        cmdd?.post(tap: .cghidEventTap)
        cmdu?.post(tap: .cghidEventTap)

        return true
    }

    func synthesizePaste() -> Bool {
        let src = CGEventSource(stateID: .hidSystemState)
        guard let cmdd = CGEvent(keyboardEventSource: src, virtualKey: 0x09, keyDown: true),
              let cmdu = CGEvent(keyboardEventSource: src, virtualKey: 0x09, keyDown: false) else {
            return false
        }

        cmdd.flags = .maskCommand
        cmdu.flags = .maskCommand

        cmdd.post(tap: .cghidEventTap)
        cmdu.post(tap: .cghidEventTap)

        return true
    }

    @discardableResult
    func writeString(_ string: String) -> Int {
        let pb = NSPasteboard.general
        pb.clearContents()
        pb.setString(string, forType: .string)
        return pb.changeCount
    }

    func readString() -> String? {
        let pb = NSPasteboard.general
        return pb.string(forType: .string)
    }
}

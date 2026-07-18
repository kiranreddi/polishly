import Cocoa

/// A small native event tap keeps the rewrite command reliable in apps whose input
/// fields consume shortcuts before a menu-bar app can see them.
final class ShortcutManager {
    static let shared = ShortcutManager()

    private var eventTap: CFMachPort?
    private var runLoopSource: CFRunLoopSource?
    private var handler: (() -> Void)?

    private init() {}

    func start(handler: @escaping () -> Void) {
        self.handler = handler
        guard eventTap == nil else { return }

        let mask = CGEventMask(1 << CGEventType.keyDown.rawValue)
        let callback: CGEventTapCallBack = { _, type, event, userInfo in
            guard let userInfo else {
                return Unmanaged.passUnretained(event)
            }
            let manager = Unmanaged<ShortcutManager>.fromOpaque(userInfo).takeUnretainedValue()
            if type == .tapDisabledByTimeout || type == .tapDisabledByUserInput {
                manager.reenableEventTap()
                return Unmanaged.passUnretained(event)
            }
            guard type == .keyDown else {
                return Unmanaged.passUnretained(event)
            }
            let isSpace = event.getIntegerValueField(.keyboardEventKeycode) == 49
            let flags = event.flags
            // Require exactly Control-Option; extra Command/Shift means a
            // different shortcut that should reach the frontmost app.
            let matches = isSpace
                && flags.contains(.maskControl)
                && flags.contains(.maskAlternate)
                && !flags.contains(.maskCommand)
                && !flags.contains(.maskShift)
            guard matches else { return Unmanaged.passUnretained(event) }
            DispatchQueue.main.async { manager.handler?() }
            return nil // consume the shortcut; never let it replace selected text
        }

        let pointer = Unmanaged.passUnretained(self).toOpaque()
        guard let tap = CGEvent.tapCreate(
            tap: .cgSessionEventTap,
            place: .headInsertEventTap,
            options: .defaultTap,
            eventsOfInterest: mask,
            callback: callback,
            userInfo: pointer
        ) else {
            return
        }

        eventTap = tap
        let source = CFMachPortCreateRunLoopSource(kCFAllocatorDefault, tap, 0)
        runLoopSource = source
        CFRunLoopAddSource(CFRunLoopGetMain(), source, .commonModes)
        CGEvent.tapEnable(tap: tap, enable: true)
    }

    func stop() {
        if let runLoopSource {
            CFRunLoopRemoveSource(CFRunLoopGetMain(), runLoopSource, .commonModes)
        }
        if let eventTap {
            CFMachPortInvalidate(eventTap)
        }
        runLoopSource = nil
        eventTap = nil
        handler = nil
    }

    private func reenableEventTap() {
        guard let eventTap else { return }
        CGEvent.tapEnable(tap: eventTap, enable: true)
    }
}

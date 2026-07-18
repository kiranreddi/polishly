import Cocoa
import Carbon.HIToolbox
import os

/// Uses the macOS global-hotkey API so focused editors cannot consume the
/// shortcut before Polishly sees it. Unlike a keyboard event tap, registration
/// does not race Accessibility permission changes.
final class ShortcutManager {
    static let shared = ShortcutManager()
    private let logger = Logger(subsystem: "com.polishly.Polishly", category: "Shortcut")

    private static let signature: OSType = 0x504C5348 // "PLSH"
    private static let identifier: UInt32 = 1

    private var hotKeyRef: EventHotKeyRef?
    private var eventHandlerRef: EventHandlerRef?
    private var handler: (() -> Void)?

    private init() {}

    func start(handler: @escaping () -> Void) {
        self.handler = handler
        installEventHandlerIfNeeded()
        guard hotKeyRef == nil else { return }

        let hotKeyID = EventHotKeyID(
            signature: Self.signature,
            id: Self.identifier
        )
        let modifiers = UInt32(controlKey | optionKey)
        let status = RegisterEventHotKey(
            UInt32(kVK_Space),
            modifiers,
            hotKeyID,
            GetApplicationEventTarget(),
            0,
            &hotKeyRef
        )
        if status != noErr {
            hotKeyRef = nil
            logger.error("Global shortcut registration failed with status: \(status)")
        } else {
            logger.info("Registered Control-Option-Space")
        }
    }

    func stop() {
        if let hotKeyRef {
            UnregisterEventHotKey(hotKeyRef)
        }
        if let eventHandlerRef {
            RemoveEventHandler(eventHandlerRef)
        }
        hotKeyRef = nil
        eventHandlerRef = nil
        handler = nil
    }

    private func installEventHandlerIfNeeded() {
        guard eventHandlerRef == nil else { return }

        var eventType = EventTypeSpec(
            eventClass: OSType(kEventClassKeyboard),
            eventKind: UInt32(kEventHotKeyPressed)
        )
        let pointer = Unmanaged.passUnretained(self).toOpaque()
        let status = InstallEventHandler(
            GetApplicationEventTarget(),
            { _, event, userData in
                guard let event, let userData else { return OSStatus(eventNotHandledErr) }
                var hotKeyID = EventHotKeyID()
                let result = GetEventParameter(
                    event,
                    EventParamName(kEventParamDirectObject),
                    EventParamType(typeEventHotKeyID),
                    nil,
                    MemoryLayout<EventHotKeyID>.size,
                    nil,
                    &hotKeyID
                )
                guard result == noErr,
                      hotKeyID.signature == ShortcutManager.signature,
                      hotKeyID.id == ShortcutManager.identifier else {
                    return OSStatus(eventNotHandledErr)
                }

                let manager = Unmanaged<ShortcutManager>
                    .fromOpaque(userData)
                    .takeUnretainedValue()
                manager.logger.info("Received Control-Option-Space")
                DispatchQueue.main.async { manager.handler?() }
                return noErr
            },
            1,
            &eventType,
            pointer,
            &eventHandlerRef
        )
        if status != noErr {
            eventHandlerRef = nil
            logger.error("Global shortcut event handler failed with status: \(status)")
        }
    }
}

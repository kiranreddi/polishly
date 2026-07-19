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

    /// Only ever called from `applicationDidFinishLaunching`, already on the main actor.
    @MainActor
    func start(handler: @escaping () -> Void) {
        self.handler = handler
        installEventHandlerIfNeeded()
        registerGlobalShortcut()
    }

    /// Only ever called from `applicationDidFinishLaunching` and a SwiftUI
    /// Button action — both already on the main actor, matching `AppState`.
    @MainActor
    func registerGlobalShortcut() {
        guard handler != nil else { return } // Wait until handler is set

        // A modifier-less hotkey would swallow a plain key (e.g. Space) in
        // every app system-wide. Refuse and keep the current shortcut active.
        guard AppState.shared.shortcutModifiers != 0 else {
            logger.error("Refusing to register a modifier-less global shortcut")
            AppState.shared.hasShortcutConflict = true
            return
        }

        // Backup the current hotkey just in case the new one fails
        let previousHotKeyRef = self.hotKeyRef
        self.hotKeyRef = nil

        if let previousHotKeyRef {
            UnregisterEventHotKey(previousHotKeyRef)
        }

        let hotKeyID = EventHotKeyID(
            signature: Self.signature,
            id: Self.identifier
        )

        let keyCode = UInt32(AppState.shared.shortcutKeyCode)
        let modifiers = UInt32(AppState.shared.shortcutModifiers)

        let status = RegisterEventHotKey(
            keyCode,
            modifiers,
            hotKeyID,
            GetApplicationEventTarget(),
            0,
            &hotKeyRef
        )

        if status != noErr {
            logger.error("Global shortcut registration failed with status: \(status)")
            AppState.shared.hasShortcutConflict = true

            // Re-register the previous working shortcut. A stored 0 could never have
            // actually registered, so never trust it as a fallback.
            if previousHotKeyRef != nil,
               let prevKeyCode = AppState.shared.lastWorkingShortcutKeyCode,
               let prevModifiers = AppState.shared.lastWorkingShortcutModifiers,
               prevModifiers != 0 {

                let fallbackStatus = RegisterEventHotKey(
                    UInt32(prevKeyCode),
                    UInt32(prevModifiers),
                    hotKeyID,
                    GetApplicationEventTarget(),
                    0,
                    &hotKeyRef
                )
                if fallbackStatus != noErr {
                    self.hotKeyRef = nil
                }
            }
        } else {
            logger.info("Registered shortcut")
            AppState.shared.hasShortcutConflict = false
            // Save as the last known working shortcut
            AppState.shared.lastWorkingShortcutKeyCode = Int(keyCode)
            AppState.shared.lastWorkingShortcutModifiers = Int(modifiers)
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
                manager.logger.info("Received Global Shortcut")
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

import Foundation
import Cocoa
import Combine

@MainActor
class SelectionObserver {
    static let shared = SelectionObserver()
    
    private var axObserver: AXObserver?
    private var isStarted = false
    private var eventMonitorToken: Any?
    private var currentlyObservedFocusedElement: AXUIElement?
    
    private var pollingTimer: AnyCancellable?
    internal struct SelectionSignature: Equatable {
        let text: String
        let bounds: CGRect?
        let pid: pid_t
    }
    internal var lastSelectionSignature: SelectionSignature?
    
    private var mouseDownLocation: NSPoint?
    private let dragThreshold: CGFloat = 5.0
    
    private var cancellables = Set<AnyCancellable>()
    
    private init() {
        // Observe trust and settings changes to stop/start or dismiss trigger
        Publishers.CombineLatest3(
            AppState.shared.$isAccessibilityTrusted,
            AppState.shared.$isPaused,
            NotificationCenter.default.publisher(for: UserDefaults.didChangeNotification)
                .prepend(Notification(name: UserDefaults.didChangeNotification))
        )
        .receive(on: DispatchQueue.main)
        .sink { [weak self] trusted, paused, _ in
            guard let self = self else { return }
            
            // Dismiss trigger if paused or trust lost
            if paused || !trusted {
                DiscoveryTriggerController.shared.hide()
            }
            
            // If trusted and not paused, we should be started, else stopped
            if trusted && !paused {
                self.start()
            } else {
                self.stop()
            }
        }
        .store(in: &cancellables)
        
        NSWorkspace.shared.notificationCenter.publisher(for: NSWorkspace.didActivateApplicationNotification)
            .receive(on: DispatchQueue.main)
            .sink { [weak self] notification in
                guard let self = self, self.isStarted else { return }
                if let app = notification.userInfo?[NSWorkspace.applicationUserInfoKey] as? NSRunningApplication {
                    self.setupAXObserver(for: app)
                }
            }
            .store(in: &cancellables)
    }
    
    func start() {
        if isStarted {
            setupAXObserver(for: NSWorkspace.shared.frontmostApplication)
            return
        }
        
        // Workspace observer is now unconditionally setup in init()
        
        // Fallback Mouse Observer for Electron apps
        setupFallbackMouseSelection()
        
        isStarted = true
        
        // Start polling fallback
        pollingTimer?.cancel()
        pollingTimer = Timer.publish(every: 0.5, on: .main, in: .common)
            .autoconnect()
            .sink { [weak self] _ in
                self?.pollAXSelection()
            }
        
        // Initial setup for the currently active app
        setupAXObserver(for: NSWorkspace.shared.frontmostApplication)
    }
    

    private func setupAXObserver(for app: NSRunningApplication?) {
        // App changed or observer reset, clear signature to allow clean discovery
        lastSelectionSignature = nil
        
        if let oldObserver = axObserver {
            CFRunLoopRemoveSource(CFRunLoopGetCurrent(), AXObserverGetRunLoopSource(oldObserver), .defaultMode)
            self.axObserver = nil
        }
        
        guard let app = app else { return }
        let pid = app.processIdentifier
        guard pid != 0 else { return }
        
        // Only attach if enabled
        guard AppState.shared.isEnabled(for: app.bundleIdentifier) else {
            DiscoveryTriggerController.shared.hide()
            return
        }
        
        // Native AX Observer
        var observerOut: AXObserver?
        let result = AXObserverCreate(pid, { observer, axElement, notification, userData in
            let observerRef = Unmanaged<SelectionObserver>.fromOpaque(userData!).takeUnretainedValue()
            let notifString = notification as String
            
            if notifString == kAXFocusedUIElementChangedNotification as String {
                observerRef.updateFocusedElement(observer: observer, applicationElement: axElement)
            } else if notifString == kAXSelectedTextChangedNotification as String || notifString == kAXValueChangedNotification as String {
                observerRef.handleAXSelectionChange(element: axElement)
            }
        }, &observerOut)
        
        if result == .success, let observer = observerOut {
            self.axObserver = observer
            let element = AXUIElementCreateApplication(pid)
            
            AXObserverAddNotification(observer, element, kAXFocusedUIElementChangedNotification as CFString, Unmanaged.passUnretained(self).toOpaque())
            AXObserverAddNotification(observer, element, kAXSelectedTextChangedNotification as CFString, Unmanaged.passUnretained(self).toOpaque())
            AXObserverAddNotification(observer, element, kAXValueChangedNotification as CFString, Unmanaged.passUnretained(self).toOpaque())
            
            CFRunLoopAddSource(CFRunLoopGetCurrent(), AXObserverGetRunLoopSource(observer), .defaultMode)
            
            // Immediately attempt to observe the currently focused element
            updateFocusedElement(observer: observer, applicationElement: element)
        }
    }
    
    private func updateFocusedElement(observer: AXObserver, applicationElement: AXUIElement) {
        if let oldElement = currentlyObservedFocusedElement {
            AXObserverRemoveNotification(observer, oldElement, kAXSelectedTextChangedNotification as CFString)
            AXObserverRemoveNotification(observer, oldElement, kAXValueChangedNotification as CFString)
            currentlyObservedFocusedElement = nil
        }
        
        var focusedRef: CFTypeRef?
        if AXUIElementCopyAttributeValue(applicationElement, kAXFocusedUIElementAttribute as CFString, &focusedRef) == .success, let focusedElement = focusedRef {
            let axFocused = focusedElement as! AXUIElement
            AXObserverAddNotification(observer, axFocused, kAXSelectedTextChangedNotification as CFString, Unmanaged.passUnretained(self).toOpaque())
            AXObserverAddNotification(observer, axFocused, kAXValueChangedNotification as CFString, Unmanaged.passUnretained(self).toOpaque())
            currentlyObservedFocusedElement = axFocused
            
            // Immediately check the selection of the newly focused element
            handleAXSelectionChange(element: axFocused)
        }
    }
    
    func stop() {
        guard isStarted else { return }
        
        DiscoveryTriggerController.shared.hide()
        
        if let observer = axObserver {
            if let oldElement = currentlyObservedFocusedElement {
                AXObserverRemoveNotification(observer, oldElement, kAXSelectedTextChangedNotification as CFString)
                AXObserverRemoveNotification(observer, oldElement, kAXValueChangedNotification as CFString)
            }
            currentlyObservedFocusedElement = nil
            CFRunLoopRemoveSource(CFRunLoopGetCurrent(), AXObserverGetRunLoopSource(observer), .defaultMode)
            self.axObserver = nil
        }
        
        if let token = eventMonitorToken {
            NSEvent.removeMonitor(token)
            self.eventMonitorToken = nil
        }
        
        
        pollingTimer?.cancel()
        pollingTimer = nil
        lastSelectionSignature = nil
        
        mouseDownLocation = nil
        isStarted = false
    }
    
    private func setupFallbackMouseSelection() {
        if let token = eventMonitorToken {
            NSEvent.removeMonitor(token)
        }
        
        eventMonitorToken = NSEvent.addGlobalMonitorForEvents(matching: [.leftMouseDown, .leftMouseUp]) { [weak self] event in
            guard let self = self else { return }
            let frontApp = NSWorkspace.shared.frontmostApplication?.bundleIdentifier
            guard AppState.shared.isEnabled(for: frontApp) else {
                self.mouseDownLocation = nil
                return
            }
            
            // Suppress clicks inside our own app windows (like PopupCard or Settings)
            if let window = NSApp.window(withWindowNumber: event.windowNumber) {
                if window.level == .floating || window.title == "Settings" { return }
            }
            
            if event.type == .leftMouseDown {
                self.mouseDownLocation = NSEvent.mouseLocation
                // Hide existing trigger when dragging starts
                DiscoveryTriggerController.shared.hide()
            } else if event.type == .leftMouseUp {
                guard let downLoc = self.mouseDownLocation else { return }
                let upLoc = NSEvent.mouseLocation
                let distance = self.calculateDistance(from: downLoc, to: upLoc)
                
                self.mouseDownLocation = nil
                
                if distance > self.dragThreshold {
                    // It was a drag. Check if there's text.
                    // We don't force clipboard yet, just show the trigger!
                    // If the user clicks the trigger, we'll synthesize Cmd+C
                    let point = NSEvent.mouseLocation
                    DiscoveryTriggerController.shared.show(at: point, capture: nil, isAX: false)
                }
            }
        }
    }
    

    
    private func handleAXSelectionChange(element: AXUIElement) {
        let frontApp = NSWorkspace.shared.frontmostApplication
        let bundleId = frontApp?.bundleIdentifier
        guard AppState.shared.isEnabled(for: bundleId) else { return }
        let pid = frontApp?.processIdentifier ?? 0
        
        if let text = AccessibilityManager.shared.getSelectedText(from: element), !text.isEmpty {
            let bounds = AccessibilityManager.shared.getSelectionBounds(from: element)
            let currentSignature = SelectionSignature(text: text, bounds: bounds, pid: pid)
            lastSelectionSignature = currentSignature
            
            let preferClipboard = AppCapabilityManager.shared.prefersClipboardInteraction(for: bundleId)
            // Show the trigger from AX discovery, but for Electron hosts defer
            // the real capture to Cmd+C on click (isAX: false).
            let capture: SelectionEngine.CapturedText? = preferClipboard
                ? nil
                : SelectionEngine.CapturedText(
                    text: text,
                    method: .accessibility,
                    bounds: bounds,
                    axElement: element,
                    sourceBundleIdentifier: bundleId
                )
            let point = bounds != nil ? NSPoint(x: bounds!.maxX, y: bounds!.minY) : NSEvent.mouseLocation
            DiscoveryTriggerController.shared.show(at: point, capture: capture, isAX: !preferClipboard)
        } else {
            lastSelectionSignature = nil
            DiscoveryTriggerController.shared.hide()
        }
    }
    
    private func pollAXSelection() {
        guard isStarted else { return }
        
        let frontApp = NSWorkspace.shared.frontmostApplication
        let bundleId = frontApp?.bundleIdentifier
        guard AppState.shared.isEnabled(for: bundleId) else {
            if lastSelectionSignature != nil {
                lastSelectionSignature = nil
                DiscoveryTriggerController.shared.hide()
            }
            return
        }
        
        guard let element = AccessibilityManager.shared.getFocusedElement() else {
            if lastSelectionSignature != nil {
                lastSelectionSignature = nil
                DiscoveryTriggerController.shared.hide()
            }
            return
        }
        
        let text = AccessibilityManager.shared.getSelectedText(from: element) ?? ""
        if text.isEmpty {
            if lastSelectionSignature != nil {
                lastSelectionSignature = nil
                DiscoveryTriggerController.shared.hide()
            }
            return
        }
        
        let bounds = AccessibilityManager.shared.getSelectionBounds(from: element)
        let pid = frontApp?.processIdentifier ?? 0
        let currentSignature = SelectionSignature(text: text, bounds: bounds, pid: pid)
        
        if lastSelectionSignature != currentSignature {
            lastSelectionSignature = currentSignature
            
            let preferClipboard = AppCapabilityManager.shared.prefersClipboardInteraction(for: bundleId)
            let capture: SelectionEngine.CapturedText? = preferClipboard
                ? nil
                : SelectionEngine.CapturedText(
                    text: text,
                    method: .accessibility,
                    bounds: bounds,
                    axElement: element,
                    sourceBundleIdentifier: bundleId
                )
            let point = bounds != nil ? NSPoint(x: bounds!.maxX, y: bounds!.minY) : NSEvent.mouseLocation
            DiscoveryTriggerController.shared.show(at: point, capture: capture, isAX: !preferClipboard)
        }
    }
    
    internal func calculateDistance(from: NSPoint, to: NSPoint) -> CGFloat {
        let dx = to.x - from.x
        let dy = to.y - from.y
        return sqrt(dx*dx + dy*dy)
    }
    
    // MARK: - Test Hooks
    
    internal var test_isStarted: Bool { isStarted }
    internal var test_hasEventMonitor: Bool { eventMonitorToken != nil }
    internal var test_hasAXObserver: Bool { axObserver != nil }
    internal var test_currentlyObservedFocusedElement: AXUIElement? { currentlyObservedFocusedElement }
    internal var test_pollingTimerActive: Bool { pollingTimer != nil }
    internal func test_pollAXSelection() { pollAXSelection() }
    
    internal var test_lastSelectionSignature: SelectionSignature? {
        get { lastSelectionSignature }
        set { lastSelectionSignature = newValue }
    }
    
    internal var test_mouseDownLocation: NSPoint? {
        get { mouseDownLocation }
        set { mouseDownLocation = newValue }
    }
}

import Foundation
import ApplicationServices
import Cocoa

class AccessibilityManager {
    static let shared = AccessibilityManager()

    private init() {}

    func getFocusedElement() -> AXUIElement? {
        let systemWideElement = AXUIElementCreateSystemWide()
        var focusedElement: CFTypeRef?

        let result = AXUIElementCopyAttributeValue(systemWideElement, kAXFocusedUIElementAttribute as CFString, &focusedElement)

        if result == .success, let elementRef = focusedElement {
            let element = elementRef as! AXUIElement
            var roleRef: CFTypeRef?
            var subroleRef: CFTypeRef?

            AXUIElementCopyAttributeValue(element, kAXRoleAttribute as CFString, &roleRef)
            AXUIElementCopyAttributeValue(element, kAXSubroleAttribute as CFString, &subroleRef)

            let role = roleRef as? String ?? ""
            let subrole = subroleRef as? String ?? ""

            if role == "AXSecureTextField" || subrole == "AXSecureTextField" {
                return nil
            }

            return element
        }
        return nil
    }

    func getSelectedText(from element: AXUIElement) -> String? {
        var selectedText: CFTypeRef?
        let result = AXUIElementCopyAttributeValue(element, kAXSelectedTextAttribute as CFString, &selectedText)

        if result == .success, let text = selectedText as? String {
            return text
        }
        return nil
    }

    func replaceSelectedText(in element: AXUIElement, with newText: String) -> Bool {
        let result = AXUIElementSetAttributeValue(element, kAXSelectedTextAttribute as CFString, newText as CFTypeRef)
        return result == .success
    }

    func getSelectionBounds(from element: AXUIElement) -> CGRect? {
        // First get the selected range
        var selectedRange: CFTypeRef?
        let rangeResult = AXUIElementCopyAttributeValue(element, kAXSelectedTextRangeAttribute as CFString, &selectedRange)

        guard rangeResult == .success, let selectedRange else { return nil }
        let rangeValue = selectedRange as! AXValue

        var range: CFRange = CFRange()
        AXValueGetValue(rangeValue, .cfRange, &range)

        // Then get the bounds for that range
        var boundsValue: CFTypeRef?
        let boundsResult = AXUIElementCopyParameterizedAttributeValue(element, kAXBoundsForRangeParameterizedAttribute as CFString, rangeValue, &boundsValue)

        guard boundsResult == .success, let boundsValue else { return nil }
        let rectValue = boundsValue as! AXValue

        var rect: CGRect = .zero
        AXValueGetValue(rectValue, .cgRect, &rect)
        return rect
    }
}

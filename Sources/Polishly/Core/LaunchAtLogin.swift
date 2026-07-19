import Foundation
import ServiceManagement

/// Launch-at-login via `SMAppService` (macOS 13+). No deprecated login-item APIs.
enum LaunchAtLogin {
    enum Status: Equatable {
        case enabled
        case disabled
        case requiresApproval
        case notFound
        case unknown
    }

    static var status: Status {
        switch SMAppService.mainApp.status {
        case .enabled: return .enabled
        case .notRegistered: return .disabled
        case .requiresApproval: return .requiresApproval
        case .notFound: return .notFound
        @unknown default: return .unknown
        }
    }

    static var isEnabled: Bool { status == .enabled }

    /// Enable or disable opening Polishly at login. Clean disable unregisters the service.
    @discardableResult
    static func setEnabled(_ enabled: Bool) throws -> Status {
        if enabled {
            if SMAppService.mainApp.status == .enabled { return .enabled }
            try SMAppService.mainApp.register()
        } else {
            // unregister is a no-op-ish clean path when already off
            if SMAppService.mainApp.status != .notRegistered {
                try SMAppService.mainApp.unregister()
            }
        }
        return status
    }

    static var userFacingMessage: String {
        switch status {
        case .enabled:
            return "Polishly opens automatically when you log in."
        case .disabled:
            return "Polishly stays off until you open it yourself."
        case .requiresApproval:
            return "macOS needs approval — open System Settings → General → Login Items and allow Polishly."
        case .notFound:
            return "Launch-at-login isn’t available for this install. Move Polishly to /Applications and try again."
        case .unknown:
            return "Couldn’t read launch-at-login status."
        }
    }
}

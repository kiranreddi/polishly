import Foundation

struct AppCapabilityManager {
    static let shared = AppCapabilityManager()
    
    enum AppGroup: String {
        case notes = "com.apple.Notes"
        case teams = "com.microsoft.teams2"
        case mail = "com.apple.mail"
        case outlook = "com.microsoft.Outlook"
        case slack = "com.tinyspeck.slackmacgap"
        case safari = "com.apple.Safari"
        case chrome = "com.google.Chrome"
        case edge = "com.microsoft.edgemac"
        
        var displayName: String {
            switch self {
            case .notes: return "Apple Notes"
            case .teams: return "Microsoft Teams"
            case .mail: return "Apple Mail"
            case .outlook: return "Microsoft Outlook"
            case .slack: return "Slack"
            case .safari: return "Safari"
            case .chrome: return "Google Chrome"
            case .edge: return "Microsoft Edge"
            }
        }

        /// Electron / Chromium hosts where AX selection range + set-selected-text
        /// are unreliable. Promise A should use clipboard copy/paste (Tier B).
        var prefersClipboardInteraction: Bool {
            switch self {
            case .teams, .slack:
                return true
            case .notes, .mail, .outlook, .safari, .chrome, .edge:
                return false
            }
        }
    }

    /// Legacy Teams bundle ID still appears on some installs.
    private static let teamsBundleAliases: Set<String> = [
        "com.microsoft.teams2",
        "com.microsoft.teams"
    ]

    /// Whether selection capture + Accept should prefer Cmd+C / Cmd+V over AX.
    func prefersClipboardInteraction(for bundleIdentifier: String?) -> Bool {
        guard let bundleIdentifier else { return false }
        if Self.teamsBundleAliases.contains(bundleIdentifier) {
            return true
        }
        return AppGroup(rawValue: bundleIdentifier)?.prefersClipboardInteraction ?? false
    }
    
    private let sensitiveAppDenylist: Set<String> = [
        "com.agilebits.onepassword7",
        "com.agilebits.onepassword-osx",
        "com.bitwarden.desktop",
        "com.apple.keychainaccess",
        "com.apple.Passwords",
        "com.lastpass.LastPass"
    ]
    
    private let userDefaults: UserDefaults

    init(userDefaults: UserDefaults = .standard) {
        self.userDefaults = userDefaults
        
        // Migrate legacy keys
        if userDefaults.object(forKey: "notesEnabled") != nil {
            let oldVal = userDefaults.bool(forKey: "notesEnabled")
            userDefaults.set(oldVal, forKey: "enabled_com.apple.Notes")
            userDefaults.removeObject(forKey: "notesEnabled")
        }
        if userDefaults.object(forKey: "teamsEnabled") != nil {
            let oldVal = userDefaults.bool(forKey: "teamsEnabled")
            userDefaults.set(oldVal, forKey: "enabled_com.microsoft.teams2")
            userDefaults.removeObject(forKey: "teamsEnabled")
        }
    }
    
    func isEnabled(for bundleIdentifier: String?, isPaused: Bool) -> Bool {
        guard !isPaused else { return false }
        guard let bundleId = bundleIdentifier else { return false }
        
        if sensitiveAppDenylist.contains(bundleId) {
            return false
        }
        
        // Handle specific app cases
        if bundleId == "com.microsoft.teams" {
            if userDefaults.object(forKey: "enabled_com.microsoft.teams2") == nil {
                return true
            }
            return userDefaults.bool(forKey: "enabled_com.microsoft.teams2")
        }
        
        // Check if it's in our known app groups
        if AppGroup(rawValue: bundleId) != nil {
            // Default to true if not set (or we can use registerDefaults)
            if userDefaults.object(forKey: "enabled_\(bundleId)") == nil {
                return true
            }
            return userDefaults.bool(forKey: "enabled_\(bundleId)")
        }
        
        // "Other apps" catch-all
        return userDefaults.bool(forKey: "enabled_other_apps") // default false if not set
    }
}

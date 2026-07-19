import XCTest
@testable import Polishly

final class AppCapabilityManagerTests: XCTestCase {
    
    var defaults: UserDefaults!
    var manager: AppCapabilityManager!
    
    override func setUp() {
        super.setUp()
        defaults = UserDefaults(suiteName: "AppCapabilityManagerTests")
        defaults.removePersistentDomain(forName: "AppCapabilityManagerTests")
        manager = AppCapabilityManager(userDefaults: defaults)
    }
    
    func testSensitiveAppDenylist() {
        // Always disabled regardless of settings
        XCTAssertFalse(manager.isEnabled(for: "com.agilebits.onepassword7", isPaused: false))
        XCTAssertFalse(manager.isEnabled(for: "com.agilebits.onepassword-osx", isPaused: false))
        XCTAssertFalse(manager.isEnabled(for: "com.bitwarden.desktop", isPaused: false))
        XCTAssertFalse(manager.isEnabled(for: "com.apple.keychainaccess", isPaused: false))
        XCTAssertFalse(manager.isEnabled(for: "com.apple.Passwords", isPaused: false))
        XCTAssertFalse(manager.isEnabled(for: "com.lastpass.LastPass", isPaused: false))
    }
    
    func testPausedState() {
        // If paused, everything is disabled
        XCTAssertFalse(manager.isEnabled(for: "com.apple.Notes", isPaused: true))
        XCTAssertFalse(manager.isEnabled(for: "com.microsoft.teams2", isPaused: true))
    }
    
    func testKnownAppsDefaultEnabled() {
        XCTAssertTrue(manager.isEnabled(for: "com.apple.Notes", isPaused: false))
        XCTAssertTrue(manager.isEnabled(for: "com.microsoft.teams2", isPaused: false))
        XCTAssertTrue(manager.isEnabled(for: "com.apple.mail", isPaused: false))
    }
    
    func testKnownAppsCanBeDisabled() {
        defaults.set(false, forKey: "enabled_com.apple.Notes")
        XCTAssertFalse(manager.isEnabled(for: "com.apple.Notes", isPaused: false))
    }
    
    func testOtherAppsDefaultDisabled() {
        // Some random app
        XCTAssertFalse(manager.isEnabled(for: "com.some.unknown.app", isPaused: false))
    }
    
    func testOtherAppsCanBeEnabled() {
        defaults.set(true, forKey: "enabled_other_apps")
        XCTAssertTrue(manager.isEnabled(for: "com.some.unknown.app", isPaused: false))
    }
    
    func testTeamsAlias() {
        // Defaults true
        XCTAssertTrue(manager.isEnabled(for: "com.microsoft.teams", isPaused: false))
        
        defaults.set(false, forKey: "enabled_com.microsoft.teams2")
        XCTAssertFalse(manager.isEnabled(for: "com.microsoft.teams", isPaused: false))
    }

    func testClipboardPreferredHosts() {
        XCTAssertTrue(manager.prefersClipboardInteraction(for: "com.microsoft.teams2"))
        XCTAssertTrue(manager.prefersClipboardInteraction(for: "com.microsoft.teams"))
        XCTAssertTrue(manager.prefersClipboardInteraction(for: "com.tinyspeck.slackmacgap"))
        XCTAssertFalse(manager.prefersClipboardInteraction(for: "com.apple.Notes"))
        XCTAssertFalse(manager.prefersClipboardInteraction(for: "com.apple.mail"))
        XCTAssertFalse(manager.prefersClipboardInteraction(for: nil))
    }
    
    func testMigration() {
        defaults.set(false, forKey: "notesEnabled")
        defaults.set(false, forKey: "teamsEnabled")
        
        let migrationManager = AppCapabilityManager(userDefaults: defaults)
        
        XCTAssertFalse(migrationManager.isEnabled(for: "com.apple.Notes", isPaused: false))
        XCTAssertFalse(migrationManager.isEnabled(for: "com.microsoft.teams2", isPaused: false))
        XCTAssertNil(defaults.object(forKey: "notesEnabled"))
        XCTAssertNil(defaults.object(forKey: "teamsEnabled"))
    }
}

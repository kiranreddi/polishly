import Foundation
import Security
import LocalAuthentication

enum KeychainReadResult {
    case success(Data)
    case notFound
    case interactionRequired
    case failure(OSStatus)
}

class KeychainHelper {
    static let shared = KeychainHelper()

    private init() {}

    @discardableResult
    func save(_ data: Data, service: String, account: String) -> Bool {
        let query = [
            kSecValueData: data,
            kSecClass: kSecClassGenericPassword,
            kSecAttrService: service,
            kSecAttrAccount: account
        ] as CFDictionary

        var status = SecItemAdd(query, nil)

        if status == errSecDuplicateItem {
            let updateQuery = [
                kSecClass: kSecClassGenericPassword,
                kSecAttrService: service,
                kSecAttrAccount: account
            ] as CFDictionary

            let updateAttributes = [kSecValueData: data] as CFDictionary
            status = SecItemUpdate(updateQuery, updateAttributes)
        }

        if status != errSecSuccess {
            print("Keychain save failed with status: \(status)")
        }
        return status == errSecSuccess
    }

    /// Silent reads never display a macOS authentication dialog. An explicit
    /// user action can opt into authentication by passing `allowInteraction`.
    func read(service: String, account: String, allowInteraction: Bool) -> KeychainReadResult {
        var query: [CFString: Any] = [
            kSecClass: kSecClassGenericPassword,
            kSecAttrService: service,
            kSecAttrAccount: account,
            kSecReturnData: true
        ]
        let authenticationContext = LAContext()
        authenticationContext.interactionNotAllowed = !allowInteraction
        query[kSecUseAuthenticationContext] = authenticationContext

        var result: AnyObject?
        let status = SecItemCopyMatching(query as CFDictionary, &result)

        switch status {
        case errSecSuccess:
            guard let data = result as? Data else { return .failure(errSecDecode) }
            return .success(data)
        case errSecItemNotFound:
            return .notFound
        case errSecInteractionNotAllowed, errSecAuthFailed, errSecUserCanceled:
            return .interactionRequired
        default:
            return .failure(status)
        }
    }

    @discardableResult
    func delete(service: String, account: String) -> Bool {
        let query = [
            kSecClass: kSecClassGenericPassword,
            kSecAttrService: service,
            kSecAttrAccount: account
        ] as CFDictionary

        let status = SecItemDelete(query)
        return status == errSecSuccess || status == errSecItemNotFound
    }
}

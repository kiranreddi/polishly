import Foundation

enum DiffToken: Equatable {
    case same(String)
    case ins(String)
    case del(String)

    var stringValue: String {
        switch self {
        case .same(let s), .ins(let s), .del(let s): return s
        }
    }

    /// True for a word (as opposed to a whitespace-only) token.
    var isWord: Bool {
        stringValue.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty == false
    }
}

class DiffEngine {
    static func tokenize(_ s: String) -> [String] {
        let pattern = "\\S+|\\s+"
        let regex = try! NSRegularExpression(pattern: pattern)
        let results = regex.matches(in: s, range: NSRange(s.startIndex..., in: s))
        return results.map {
            String(s[Range($0.range, in: s)!])
        }
    }
    
    static func diffWords(original: String, target: String) -> [DiffToken] {
        let A = tokenize(original)
        let B = tokenize(target)
        let n = A.count
        let m = B.count
        
        var dp = Array(repeating: Array(repeating: 0, count: m + 1), count: n + 1)
        
        for i in stride(from: n - 1, through: 0, by: -1) {
            for j in stride(from: m - 1, through: 0, by: -1) {
                if A[i] == B[j] {
                    dp[i][j] = dp[i + 1][j + 1] + 1
                } else {
                    dp[i][j] = max(dp[i + 1][j], dp[i][j + 1])
                }
            }
        }
        
        var out: [DiffToken] = []
        var i = 0
        var j = 0
        
        while i < n && j < m {
            if A[i] == B[j] {
                out.append(.same(A[i]))
                i += 1
                j += 1
            } else if dp[i + 1][j] >= dp[i][j + 1] {
                out.append(.del(A[i]))
                i += 1
            } else {
                out.append(.ins(B[j]))
                j += 1
            }
        }
        
        while i < n {
            out.append(.del(A[i]))
            i += 1
        }
        while j < m {
            out.append(.ins(B[j]))
            j += 1
        }
        
        return out
    }
}

import SwiftUI

struct PopupCardView: View {
    @ObservedObject var viewModel: PopupViewModel

    var body: some View {
        VStack(spacing: 0) {
            // Header
            HStack(spacing: 8) {
                RoundedRectangle(cornerRadius: 5)
                    .fill(Color(hex: "008c80"))
                    .frame(width: 16, height: 16)
                Text("Polishly")
                    .fontWeight(.semibold)
                    .font(.system(size: 12.5))
                Text("· MVP")
                    .foregroundColor(Color(hex: "6b7280"))
                    .font(.system(size: 11.5))
                Spacer()
                Button(action: {
                    viewModel.close()
                }) {
                    Text("×")
                        .font(.system(size: 16))
                        .foregroundColor(Color(hex: "6b7280"))
                        .padding(.horizontal, 5)
                        .padding(.vertical, 2)
                }
                .buttonStyle(PlainButtonStyle())
            }
            .padding(.horizontal, 16)
            .padding(.top, 12)
            .padding(.bottom, 10)

            Divider().background(Color(hex: "eef0f3"))

            // Context Disclosure
            HStack(spacing: 6) {
                Circle()
                    .fill(Color(hex: viewModel.hasContext ? "008c80" : "c8ccd4"))
                    .frame(width: 6, height: 6)
                Text(viewModel.contextMessage)
                    .font(.system(size: 11))
                    .foregroundColor(Color(hex: "6b7280"))
                Spacer()
            }
            .padding(.horizontal, 9)
            .padding(.vertical, 7)
            .background(Color(hex: "f7faf9"))
            .cornerRadius(7)
            .padding(.horizontal, 16)
            .padding(.top, 10)

            // Diff Content
            HStack(alignment: .top, spacing: 10) {
                RoundedRectangle(cornerRadius: 2)
                    .fill(Color(hex: "008c80"))
                    .frame(width: 3)

                VStack(alignment: .leading, spacing: 7) {
                    Text(viewModel.rewriteTitle)
                        .font(.system(size: 12.5))
                        .fontWeight(.semibold)
                        .foregroundColor(Color(hex: "008c80"))

                    if viewModel.isError {
                        HStack(spacing: 8) {
                            Text(viewModel.errorMessage)
                                .font(.system(size: 12.5))
                                .foregroundColor(Color(hex: "9a3412"))
                            Spacer()
                            Button("Retry") { viewModel.retry() }
                                .buttonStyle(.borderless)
                                .foregroundColor(Color(hex: "0b6b5f"))
                        }
                        .padding(10)
                        .background(Color(hex: "fff7ed"))
                        .cornerRadius(8)
                    } else if viewModel.isStreaming && viewModel.diffTokens.isEmpty {
                        // Skeleton loading state
                        VStack(alignment: .leading, spacing: 8) {
                            RoundedRectangle(cornerRadius: 6).fill(Color(hex: "eef0f3")).frame(height: 10)
                            RoundedRectangle(cornerRadius: 6).fill(Color(hex: "eef0f3")).frame(height: 10).padding(.trailing, 40)
                            RoundedRectangle(cornerRadius: 6).fill(Color(hex: "eef0f3")).frame(height: 10).padding(.trailing, 100)
                        }
                        .padding(.top, 2)
                        .padding(.bottom, 6)
                    } else {
                        // Computed Diff
                        DiffTextView(tokens: viewModel.diffTokens)
                            .font(.system(size: 13.5))
                            .lineSpacing(4)
                    }
                }
                .frame(maxWidth: .infinity, alignment: .leading)
            }
            .padding(.horizontal, 16)
            .padding(.top, 10)
            .padding(.bottom, 4)

            if viewModel.showReviseInput {
                HStack(spacing: 8) {
                    TextField("Tell Polishly what to change...", text: $viewModel.reviseText)
                        .textFieldStyle(RoundedBorderTextFieldStyle())
                        .font(.system(size: 12.5))

                    Button("Apply") {
                        viewModel.submitRevise()
                    }
                    .buttonStyle(AcceptButtonStyle(isSmall: true))

                    Button("×") {
                        viewModel.showReviseInput = false
                    }
                    .buttonStyle(PlainButtonStyle())
                }
                .padding(.horizontal, 16)
                .padding(.bottom, 12)
            }

            // Actions
            HStack(spacing: 8) {
                if viewModel.isTierC {
                    Button("Copied — Press ⌘V") {
                        viewModel.close()
                    }
                    .buttonStyle(AcceptButtonStyle(isSmall: false))
                } else if viewModel.isPasteSentUnconfirmable {
                    Button("Paste sent — verify the field") {
                        viewModel.close()
                    }
                    .buttonStyle(AcceptButtonStyle(isSmall: false))
                } else {
                    Button("Accept") {
                        viewModel.accept()
                    }
                    .buttonStyle(AcceptButtonStyle(isSmall: false))
                    .disabled(viewModel.isStreaming || viewModel.diffTokens.isEmpty || viewModel.isError)
                }

                Button(action: {
                    viewModel.showReviseInput.toggle()
                }) {
                    HStack(spacing: 5) {
                        Text("✎")
                        Text("Revise with AI")
                    }
                    .font(.system(size: 12.5, weight: .medium))
                }
                .buttonStyle(ReviseButtonStyle(isOn: viewModel.showReviseInput))
                .disabled(viewModel.isStreaming)

                Spacer()

                Button(action: {
                    viewModel.regenerate()
                }) {
                    Text("↻")
                        .font(.system(size: 14))
                }
                .buttonStyle(IconButtonStyle())
                .disabled(viewModel.isStreaming)

                Button(action: {
                    viewModel.copy()
                }) {
                    Text("⧉")
                        .font(.system(size: 14))
                }
                .buttonStyle(IconButtonStyle())
                .disabled(viewModel.isStreaming || viewModel.diffTokens.isEmpty || viewModel.isError)
            }
            .padding(.horizontal, 16)
            .padding(.vertical, 12)

            // Tabs
            VStack(spacing: 0) {
                Divider().background(Color(hex: "eef0f3"))
                HStack(spacing: 18) {
                    TabButton(title: "Improve", key: "improve", selected: $viewModel.selectedTab) { viewModel.selectTab("improve") }
                    TabButton(title: "Concise", key: "concise", selected: $viewModel.selectedTab) { viewModel.selectTab("concise") }
                    TabButton(title: "Friendly", key: "friendly", selected: $viewModel.selectedTab) { viewModel.selectTab("friendly") }
                    TabButton(title: "Expand", key: "expand", selected: $viewModel.selectedTab) { viewModel.selectTab("expand") }
                    Spacer()
                }
                .padding(.horizontal, 16)
            }
            .disabled(viewModel.isStreaming)
            .opacity(viewModel.isStreaming ? 0.4 : 1.0)
        }
        .frame(width: 430)
        .background(Color.white)
        .cornerRadius(14)
        .shadow(color: Color.black.opacity(0.15), radius: 20, y: 10)
        .colorScheme(.light) // Force light mode as per mockup or adapt
    }
}

// DiffTextView renders the tokens
struct DiffTextView: View {
    let tokens: [DiffToken]

    var body: some View {
        var text = Text("")
        for (index, token) in tokens.enumerated() {
            switch token {
            case .same(let s):
                text = text + Text(s)
            case .ins(let s):
                text = text + Text(s).foregroundColor(Color(hex: "0b6b5f"))
            case .del(let s):
                text = text + Text(s).foregroundColor(Color(hex: "b0b4bd")).strikethrough(true, color: Color(hex: "d7a3a3"))
            }

            // A del immediately followed by an ins (or vice versa) is a word-level
            // replacement — there's no whitespace token between them in either
            // source text, since both occupy the same position. Rendered back to
            // back with no gap, two real words read as one run-together word
            // (e.g. "email;" + "email" as "email;email"). Insert a plain space
            // at that boundary only — not between whitespace-only tokens, where
            // it would just add a redundant space.
            if let next = tokens[safe: index + 1],
               token.isWord, next.isWord,
               isReplacementBoundary(token, next) {
                text = text + Text(" ")
            }
        }
        return text
    }

    private func isReplacementBoundary(_ current: DiffToken, _ next: DiffToken) -> Bool {
        switch (current, next) {
        case (.del, .ins), (.ins, .del):
            return true
        default:
            return false
        }
    }
}

private extension Array {
    subscript(safe index: Int) -> Element? {
        indices.contains(index) ? self[index] : nil
    }
}

// Styles and Components
struct TabButton: View {
    let title: String
    let key: String
    @Binding var selected: String
    let action: () -> Void

    var body: some View {
        VStack(spacing: 0) {
            Text(title)
                .font(.system(size: 12.5, weight: selected == key ? .bold : .medium))
                .foregroundColor(Color(hex: selected == key ? "1c2430" : "6b7280"))
                .padding(.vertical, 11)

            Rectangle()
                .fill(selected == key ? Color(hex: "008c80") : Color.clear)
                .frame(height: 2)
        }
        .contentShape(Rectangle())
        .onTapGesture(perform: action)
    }
}

struct AcceptButtonStyle: ButtonStyle {
    let isSmall: Bool
    @Environment(\.isEnabled) private var isEnabled
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 12.5, weight: .bold))
            .foregroundColor(.white)
            .padding(.horizontal, isSmall ? 12 : 14)
            .padding(.vertical, 7)
            .background(Color(hex: "008c80").opacity(configuration.isPressed ? 0.8 : 1))
            .cornerRadius(8)
            // A disabled SwiftUI Button still fires this makeBody, so custom
            // styles must dim themselves explicitly — the default automatic
            // disabled-dimming only applies to stock button styles.
            .opacity(isEnabled ? 1 : 0.4)
    }
}

struct ReviseButtonStyle: ButtonStyle {
    let isOn: Bool
    @Environment(\.isEnabled) private var isEnabled
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .foregroundColor(Color(hex: isOn ? "0b6b5f" : "1c2430"))
            .padding(.horizontal, 14)
            .padding(.vertical, 7)
            .background(Color(hex: isOn ? "173230" : "f2f3f5").opacity(configuration.isPressed ? 0.8 : 1))
            .cornerRadius(8)
            .opacity(isEnabled ? 1 : 0.4)
    }
}

struct IconButtonStyle: ButtonStyle {
    @Environment(\.isEnabled) private var isEnabled
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .foregroundColor(Color(hex: "6b7280"))
            .frame(width: 28, height: 28)
            .background(configuration.isPressed ? Color(hex: "f2f3f5") : Color.clear)
            .cornerRadius(7)
            .opacity(isEnabled ? 1 : 0.4)
    }
}

extension Color {
    init(hex: String) {
        let hex = hex.trimmingCharacters(in: CharacterSet.alphanumerics.inverted)
        var int: UInt64 = 0
        Scanner(string: hex).scanHexInt64(&int)
        let a, r, g, b: UInt64
        switch hex.count {
        case 3: // RGB (12-bit)
            (a, r, g, b) = (255, (int >> 8) * 17, (int >> 4 & 0xF) * 17, (int & 0xF) * 17)
        case 6: // RGB (24-bit)
            (a, r, g, b) = (255, int >> 16, int >> 8 & 0xFF, int & 0xFF)
        case 8: // ARGB (32-bit)
            (a, r, g, b) = (int >> 24, int >> 16 & 0xFF, int >> 8 & 0xFF, int & 0xFF)
        default:
            (a, r, g, b) = (1, 1, 1, 0)
        }
        self.init(
            .sRGB,
            red: Double(r) / 255,
            green: Double(g) / 255,
            blue:  Double(b) / 255,
            opacity: Double(a) / 255
        )
    }
}

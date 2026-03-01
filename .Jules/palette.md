## 2024-05-22 - [WPF Accessibility: Unlabeled Inputs]
**Learning:** WPF TextBoxes and PasswordBoxes do not automatically associate with nearby TextBlocks acting as labels. Screen readers will read "TextBox" without context, making login forms inaccessible.
**Action:** Always add `AutomationProperties.Name` to input controls and icon-only buttons if they are not explicitly labeled by `AutomationProperties.LabeledBy`.
## 2026-03-01 - [Added confirmation dialogs for destructive actions]
**Learning:** Destructive actions like clearing the cache or restarting the server in the dashboard executed immediately, which can lead to accidental downtime or loss of analytics. Adding a simple confirmation dialog prevents accidental clicks.
**Action:** Always wrap destructive or disruptive commands in a confirmation dialog (e.g., `MessageBox.Show`) before execution, ensuring the user intent is explicit.

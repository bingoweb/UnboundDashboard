## 2024-05-22 - [WPF Accessibility: Unlabeled Inputs]
**Learning:** WPF TextBoxes and PasswordBoxes do not automatically associate with nearby TextBlocks acting as labels. Screen readers will read "TextBox" without context, making login forms inaccessible.
**Action:** Always add `AutomationProperties.Name` to input controls and icon-only buttons if they are not explicitly labeled by `AutomationProperties.LabeledBy`.

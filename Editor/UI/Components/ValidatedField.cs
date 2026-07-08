using UnityEngine.UIElements;

namespace Sorolla.Palette.Editor.UI
{
    /// <summary>Labeled text input with an inline valid/required/invalid indicator + helper text.</summary>
    static class ValidatedField
    {
        internal enum State
        {
            Valid,
            Required,
            Invalid,
        }

        internal static VisualElement Create(string label, string value, State state, string message = null)
        {
            var container = new VisualElement();
            container.AddToClassList("sorolla-field");

            var labelElement = new Label(label);
            labelElement.AddToClassList("sorolla-field-label");
            container.Add(labelElement);

            var inputRow = new VisualElement();
            inputRow.AddToClassList("sorolla-field-input-row");

            var textField = new TextField { value = value };
            textField.AddToClassList("sorolla-field-input");
            textField.AddToClassList(ClassFor(state));
            inputRow.Add(textField);

            var icon = new Label(IconFor(state));
            icon.AddToClassList("sorolla-field-icon");
            icon.AddToClassList(ClassFor(state));
            inputRow.Add(icon);

            container.Add(inputRow);

            if (!string.IsNullOrEmpty(message))
            {
                var messageLabel = new Label(message);
                messageLabel.AddToClassList("sorolla-field-message");
                messageLabel.AddToClassList(ClassFor(state));
                container.Add(messageLabel);
            }

            return container;
        }

        static string IconFor(State state)
        {
            switch (state)
            {
                case State.Valid: return "✓";
                case State.Invalid: return "✕";
                default: return "•";
            }
        }

        static string ClassFor(State state)
        {
            switch (state)
            {
                case State.Valid: return "sorolla-field-valid";
                case State.Invalid: return "sorolla-field-invalid";
                default: return "sorolla-field-required";
            }
        }
    }
}

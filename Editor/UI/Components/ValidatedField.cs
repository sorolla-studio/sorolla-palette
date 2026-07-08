using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Sorolla.Palette.Editor.UI
{
    /// <summary>Labeled text input with an inline valid/required/invalid indicator + helper text.</summary>
    static class ValidatedField
    {
        internal enum State
        {
            /// <summary>Optional field, empty - no icon, this is a normal state, not a problem.</summary>
            None,
            Valid,
            Required,
            Invalid,
        }

        internal static VisualElement Create(string label, string value, State state, string message = null)
        {
            var container = new VisualElement();
            container.AddToClassList("sorolla-field");
            container.Add(BuildLabel(label));

            var textField = new TextField { value = value };
            textField.AddToClassList("sorolla-field-input");
            textField.AddToClassList(ClassFor(state));

            container.Add(BuildInputRow(textField, state));
            AddMessage(container, state, message);
            return container;
        }

        /// <summary>Same accent-border/icon/message treatment as <see cref="Create"/>, but wraps a
        /// real bound PropertyField instead of building a plain TextField from a copied string -
        /// keeps the SerializedObject binding (undo, dirty-marking, multi-edit,
        /// TrackSerializedObjectValue) and the field's own label/[Header] decorator rendering
        /// byte-for-byte identical to a bare PropertyField; this only adds the border color + a
        /// trailing state icon + optional subtext. Only fits a LEAF-value property (string, bool,
        /// int) - a nested/complex property (e.g. PlatformAdUnitId) renders its own foldout and
        /// should stay a plain PropertyField instead (verified via screenshot: wrapping one here
        /// double-labels and reorders the Header decorator).</summary>
        internal static VisualElement CreateBound(SerializedProperty property, string label, State state, string message = null)
        {
            var container = new VisualElement();
            container.AddToClassList("sorolla-field");

            var propertyField = new PropertyField(property, label);
            propertyField.AddToClassList("sorolla-field-input");
            propertyField.AddToClassList(ClassFor(state));

            container.Add(BuildInputRow(propertyField, state));
            AddMessage(container, state, message);
            return container;
        }

        static Label BuildLabel(string label)
        {
            var labelElement = new Label(label);
            labelElement.AddToClassList("sorolla-field-label");
            return labelElement;
        }

        static VisualElement BuildInputRow(VisualElement field, State state)
        {
            var inputRow = new VisualElement();
            inputRow.AddToClassList("sorolla-field-input-row");
            inputRow.Add(field);

            if (state != State.None)
            {
                var icon = new Label(IconFor(state));
                icon.AddToClassList("sorolla-field-icon");
                icon.AddToClassList(ClassFor(state));
                inputRow.Add(icon);
            }

            return inputRow;
        }

        static void AddMessage(VisualElement container, State state, string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            var messageLabel = new Label(message);
            messageLabel.AddToClassList("sorolla-field-message");
            messageLabel.AddToClassList(ClassFor(state));
            container.Add(messageLabel);
        }

        static string IconFor(State state)
        {
            switch (state)
            {
                case State.Valid: return "✓";
                case State.Invalid: return "✕";
                case State.Required: return "•";
                default: return string.Empty;
            }
        }

        static string ClassFor(State state)
        {
            switch (state)
            {
                case State.Valid: return "sorolla-field-valid";
                case State.Invalid: return "sorolla-field-invalid";
                case State.Required: return "sorolla-field-required";
                default: return string.Empty;
            }
        }
    }
}

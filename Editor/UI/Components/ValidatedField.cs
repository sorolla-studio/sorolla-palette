using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Sorolla.Palette.Editor.UI
{
    /// <summary>Inline label + text input + trailing valid/required/invalid indicator, matching
    /// the standard editor form geometry (fixed label column, input fills the rest, icon trails)
    /// so ValidatedField rows sit in the same column as the stock PropertyField rows around
    /// them.</summary>
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

            var textField = new TextField { value = value };
            container.Add(BuildRow(label, textField, state));
            AddMessage(container, state, message);
            return container;
        }

        /// <summary>Same row shape as <see cref="Create"/>, but binds a real TextField to a
        /// SerializedProperty via BindProperty instead of copying the value into a plain string -
        /// keeps the SerializedObject binding (undo, dirty-marking, multi-edit,
        /// TrackSerializedObjectValue) identical to PropertyField's, since those all operate on the
        /// SerializedObject/property, not on PropertyField itself.
        ///
        /// Deliberately does NOT use PropertyField: PropertyField renders its own inline label at
        /// an auto width, so different label text produced different input-column start x across
        /// rows (measured 120px/129px/135px in the real window - a visible misalignment vs the
        /// stock PropertyField rows above). And for a property carrying a [Header] attribute,
        /// PropertyField bakes the header decorator INSIDE the same element as the field, so
        /// wrapping that whole thing in this component's icon row centered the icon across the
        /// header+field combined height - it visually floated beside the header instead of sitting
        /// on the field's own row (both caught via screenshot). A plain bound TextField has neither
        /// problem: the label here gets the same fixed width as every other row (real
        /// PropertyFields' own label column, see .sorolla-field-label in tokens.uss), and any
        /// [Header] text is the caller's job to render OUTSIDE this component, never bundled into
        /// the same row as the icon.
        ///
        /// Only fits a LEAF-value property (string, bool, int) - a nested/complex property (e.g.
        /// PlatformAdUnitId) renders its own foldout and should stay a plain PropertyField
        /// instead.</summary>
        internal static VisualElement CreateBound(SerializedProperty property, string label, State state, string message = null)
        {
            var container = new VisualElement();
            container.AddToClassList("sorolla-field");

            var textField = new TextField();
            textField.BindProperty(property);
            container.Add(BuildRow(label, textField, state));
            AddMessage(container, state, message);
            return container;
        }

        static VisualElement BuildRow(string label, TextField textField, State state)
        {
            var row = new VisualElement();
            row.AddToClassList("sorolla-field-row");

            var labelElement = new Label(label);
            labelElement.AddToClassList("sorolla-field-label");
            row.Add(labelElement);

            textField.AddToClassList("sorolla-field-input");
            textField.AddToClassList(ClassFor(state));
            row.Add(textField);

            if (state != State.None)
            {
                var icon = new Label(IconFor(state));
                icon.AddToClassList("sorolla-field-icon");
                icon.AddToClassList(ClassFor(state));
                row.Add(icon);
            }

            return row;
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

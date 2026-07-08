using System;
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
            var refs = BuildRow(container, label, textField);
            ApplyState(refs, state, message);
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
        /// The state is LIVE, not a one-shot snapshot taken at build time: <paramref
        /// name="evaluate"/> re-runs on every keystroke (TextField.RegisterValueChangedCallback
        /// fires per change on a bound field), swapping the border class/icon/subtext in place. A
        /// stale green check on a value the user has since typed garbage into is worse than no
        /// validation at all (caught live by Arthur: typing into an already-valid ad-unit field
        /// left the checkmark green).
        ///
        /// Only fits a LEAF-value property (string, bool, int) - a nested/complex property (e.g.
        /// PlatformAdUnitId) renders its own foldout and should stay a plain PropertyField
        /// instead.</summary>
        internal static VisualElement CreateBound(SerializedProperty property, string label, Func<string, (State state, string message)> evaluate)
        {
            var container = new VisualElement();
            container.AddToClassList("sorolla-field");

            var textField = new TextField();
            textField.BindProperty(property);
            var refs = BuildRow(container, label, textField);

            (State state, string message) = evaluate(property.stringValue);
            ApplyState(refs, state, message);

            textField.RegisterValueChangedCallback(evt =>
            {
                (State newState, string newMessage) = evaluate(evt.newValue);
                ApplyState(refs, newState, newMessage);
            });

            return container;
        }

        struct RowRefs
        {
            internal TextField Field;
            internal Label Icon;
            internal Label Message;
        }

        static RowRefs BuildRow(VisualElement container, string label, TextField textField)
        {
            var row = new VisualElement();
            row.AddToClassList("sorolla-field-row");

            var labelElement = new Label(label);
            labelElement.AddToClassList("sorolla-field-label");
            row.Add(labelElement);

            textField.AddToClassList("sorolla-field-input");
            row.Add(textField);

            // Always present (even for State.None, just empty/invisible) so a live state change
            // doesn't add/remove elements and shift the row's layout mid-typing.
            var icon = new Label();
            icon.AddToClassList("sorolla-field-icon");
            row.Add(icon);

            container.Add(row);

            var message = new Label();
            message.AddToClassList("sorolla-field-message");
            message.style.display = DisplayStyle.None;
            container.Add(message);

            return new RowRefs { Field = textField, Icon = icon, Message = message };
        }

        static void ApplyState(RowRefs refs, State state, string message)
        {
            string stateClass = ClassFor(state);

            SetStateClass(refs.Field, stateClass);
            SetStateClass(refs.Icon, stateClass);
            refs.Icon.text = IconFor(state);

            SetStateClass(refs.Message, stateClass);
            if (string.IsNullOrEmpty(message))
            {
                refs.Message.text = string.Empty;
                refs.Message.style.display = DisplayStyle.None;
            }
            else
            {
                refs.Message.text = message;
                refs.Message.style.display = DisplayStyle.Flex;
            }
        }

        static void SetStateClass(VisualElement element, string stateClass)
        {
            element.RemoveFromClassList("sorolla-field-valid");
            element.RemoveFromClassList("sorolla-field-invalid");
            element.RemoveFromClassList("sorolla-field-required");
            if (!string.IsNullOrEmpty(stateClass))
                element.AddToClassList(stateClass);
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

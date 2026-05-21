namespace SRDebugger.UI.Controls.Data
{
    using System;
    using SRF;
    using UnityEngine;
    using UnityEngine.UI;

    public class ReadOnlyControl : DataBoundControl
    {
        private const int FlagListTitleFontSize = 16;
        private const int FlagListValueFontSize = 18;
        private const float FlagListPreferredWidth = 520f;
        private const float FlagListMinPreferredHeight = 96f;
        private const float FlagListLineHeight = 24f;
        private const float FlagListVerticalPadding = 42f;
        private bool isFlagListProperty;

        [RequiredField]
        public Text ValueText;

        [RequiredField]
        public Text Title;

        protected override void Start()
        {
            base.Start();
        }

        protected override void OnBind(string propertyName, Type t)
        {
            base.OnBind(propertyName, t);
            Title.text = propertyName;
            ApplyFlagListStyle(propertyName);
        }

        protected override void OnValueUpdated(object newValue)
        {
            ValueText.text = Convert.ToString(newValue);
            ApplyFlagListHeight(ValueText.text);
        }

        public override bool CanBind(Type type, bool isReadOnly)
        {
            return type == typeof(string) && isReadOnly;
        }

        private void ApplyFlagListStyle(string propertyName)
        {
            isFlagListProperty = IsFlagListProperty(propertyName);
            if (!isFlagListProperty)
            {
                return;
            }

            Title.fontSize = FlagListTitleFontSize;
            ValueText.fontSize = FlagListValueFontSize;
            Title.horizontalOverflow = HorizontalWrapMode.Overflow;
            ValueText.horizontalOverflow = HorizontalWrapMode.Overflow;

            var layoutElement = GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.minWidth = FlagListPreferredWidth;
            layoutElement.preferredWidth = FlagListPreferredWidth;
            layoutElement.minHeight = FlagListMinPreferredHeight;
            layoutElement.preferredHeight = FlagListMinPreferredHeight;
        }

        private void ApplyFlagListHeight(string value)
        {
            if (!isFlagListProperty)
            {
                return;
            }

            var layoutElement = GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                return;
            }

            int lineCount = string.IsNullOrEmpty(value) ? 1 : value.Split('\n').Length;
            float preferredHeight = Mathf.Max(
                FlagListMinPreferredHeight,
                FlagListVerticalPadding + lineCount * FlagListLineHeight);

            layoutElement.minHeight = preferredHeight;
            layoutElement.preferredHeight = preferredHeight;
        }

        private static bool IsFlagListProperty(string propertyName)
        {
            return !string.IsNullOrEmpty(propertyName) &&
                   (string.Equals(propertyName, "Runtime Flags", StringComparison.Ordinal) ||
                    propertyName.StartsWith("Runtime 0", StringComparison.Ordinal) ||
                    propertyName.StartsWith("Saved 0", StringComparison.Ordinal) ||
                    string.Equals(propertyName, "Selected Slot Saved Flags", StringComparison.Ordinal));
        }
    }
}

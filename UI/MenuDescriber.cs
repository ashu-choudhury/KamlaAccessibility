using System;
using UnityEngine;
using UnityEngine.UI;
using Il2CppTMPro;

namespace KamlaAccessibility.UI
{
    /// <summary>
    /// Utility to inspect Unity UI and TextMeshPro elements and describe them for speech and Braille.
    /// </summary>
    public static class MenuDescriber
    {
        public static string DescribeElement(GameObject go)
        {
            if (go == null) return string.Empty;

            string label = GetElementText(go);
            if (string.IsNullOrEmpty(label))
            {
                label = CleanGameObjectName(go.name);
            }

            // Check for Toggle
            var toggle = go.GetComponent<Toggle>();
            if (toggle != null)
            {
                string state = toggle.isOn ? "checked" : "not checked";
                return $"{label}, toggle, {state}";
            }

            // Check for Slider
            var slider = go.GetComponent<Slider>();
            if (slider != null)
            {
                float pct = 0f;
                if (Mathf.Abs(slider.maxValue - slider.minValue) > 0.001f)
                {
                    pct = Mathf.Clamp01((slider.value - slider.minValue) / (slider.maxValue - slider.minValue)) * 100f;
                }
                return $"{label}, slider, {(int)pct} percent";
            }

            // Check for TMP_Dropdown / Dropdown
            var tmpDropdown = go.GetComponent<TMP_Dropdown>();
            if (tmpDropdown != null)
            {
                string current = tmpDropdown.captionText != null ? tmpDropdown.captionText.text : $"Option {tmpDropdown.value + 1}";
                return $"{label}, dropdown, current selection: {current}";
            }
            var dropdown = go.GetComponent<Dropdown>();
            if (dropdown != null)
            {
                string current = dropdown.captionText != null ? dropdown.captionText.text : $"Option {dropdown.value + 1}";
                return $"{label}, dropdown, current selection: {current}";
            }

            // Check for InputField
            var tmpInput = go.GetComponent<TMP_InputField>();
            if (tmpInput != null)
            {
                string content = string.IsNullOrEmpty(tmpInput.text) ? "empty" : tmpInput.text;
                return $"{label}, edit text, {content}";
            }
            var input = go.GetComponent<InputField>();
            if (input != null)
            {
                string content = string.IsNullOrEmpty(input.text) ? "empty" : input.text;
                return $"{label}, edit text, {content}";
            }

            // Check for Button
            var button = go.GetComponent<Button>();
            if (button != null)
            {
                return $"{label}, button";
            }

            // General Selectable
            var selectable = go.GetComponent<Selectable>();
            if (selectable != null)
            {
                return $"{label}, item";
            }

            return label;
        }

        public static string GetElementText(GameObject go)
        {
            if (go == null) return string.Empty;

            // Check TMP_Text in object or children
            var tmpTexts = go.GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in tmpTexts)
            {
                if (t != null && !string.IsNullOrWhiteSpace(t.text))
                {
                    return t.text.Trim();
                }
            }

            // Check standard Text
            var texts = go.GetComponentsInChildren<Text>(true);
            foreach (var t in texts)
            {
                if (t != null && !string.IsNullOrWhiteSpace(t.text))
                {
                    return t.text.Trim();
                }
            }

            return string.Empty;
        }

        private static string CleanGameObjectName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Item";

            name = name.Replace("(Clone)", "").Trim();
            name = name.Replace("Btn", " Button");
            name = name.Replace("Button", " Button");
            name = name.Replace("_", " ");

            // Insert space before uppercase letters for CamelCase names
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsWhiteSpace(name[i - 1]))
                {
                    sb.Append(' ');
                }
                sb.Append(name[i]);
            }

            return sb.ToString().Trim();
        }
    }
}

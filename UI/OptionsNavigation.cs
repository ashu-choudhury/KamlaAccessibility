using System;
using System.Collections.Generic;
using Il2Cpp;
using KamlaAccessibility.Speech;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KamlaAccessibility.UI
{
    internal sealed class OptionsNavigation
    {
        private GraphicsSettings _settings;
        private EventSystem _eventSystem;
        private bool _previousNavigation;
        private GameObject _panel;
        private Selectable _focused;
        private Selectable _returnFocus;
        private string _lastDescription;
        private readonly List<Selectable> _controls = new();

        public bool Update()
        {
            if (!Refresh()) return false;
            if (Input.GetKeyDown(KeyCode.Tab))
                Move(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1);
            else if (Input.GetKeyDown(KeyCode.UpArrow)) Move(-1);
            else if (Input.GetKeyDown(KeyCode.DownArrow)) Move(1);
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) Adjust(-1);
            else if (Input.GetKeyDown(KeyCode.RightArrow)) Adjust(1);
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                     Input.GetKeyDown(KeyCode.Space)) Activate();
            return true;
        }

        internal bool Refresh()
        {
            if (_settings == null || !_settings.isActiveAndEnabled) _settings = Object.FindObjectOfType<GraphicsSettings>();
            var events = EventSystem.current;
            var panel = GetPanel(out string title);
            if (panel == null || events == null)
            {
                Reset();
                return false;
            }
            if (_eventSystem != events)
            {
                Reset();
                _eventSystem = events;
                _previousNavigation = events.sendNavigationEvents;
            }
            events.sendNavigationEvents = false;

            if (panel != _panel)
            {
                if (_panel == _settings.graphicsSettingsCanvas) _returnFocus = _focused;
                _panel = panel;
                _focused = panel == _settings.graphicsSettingsCanvas ? _returnFocus : null;
                _lastDescription = null;
            }
            else title = null;

            _controls.Clear();
            foreach (var control in panel.GetComponentsInChildren<Selectable>())
                if (IsUsable(control) && !IsRightHalfOfSetting(control)) _controls.Add(control);
            if (_controls.Count == 0) return true;

            var selected = events.currentSelectedGameObject;
            var selectedControl = selected != null ? selected.GetComponent<Selectable>() : null;
            if (title == null && _controls.Contains(selectedControl)) _focused = selectedControl;
            if (!_controls.Contains(_focused))
            {
                _focused = panel == _settings.resetConfirmationCanvas
                    ? _controls.Find(IsResetCancelButton) ?? _controls[0]
                    : _controls[0];
            }
            if (events.currentSelectedGameObject != _focused.gameObject) _focused.Select();
            Announce(title);
            return true;
        }

        private GameObject GetPanel(out string title)
        {
            title = null;
            if (_settings == null || !_settings.isActiveAndEnabled) return null;
            if (IsVisible(_settings.resetConfirmationCanvas))
            {
                title = "Reset save files?";
                return _settings.resetConfirmationCanvas;
            }
            if (IsVisible(_settings.saveConfirmationCanvas))
            {
                title = _settings.SaveSettingsAsk != null ? _settings.SaveSettingsAsk.text : "Save settings?";
                return _settings.saveConfirmationCanvas;
            }
            if (!IsVisible(_settings.graphicsSettingsCanvas)) return null;
            title = "Options. Up and Down browse. Left and Right adjust";
            return _settings.graphicsSettingsCanvas;
        }

        internal void Move(int direction)
        {
            if (_controls.Count == 0) return;
            int next = (_controls.IndexOf(_focused) + direction + _controls.Count) % _controls.Count;
            _focused = _controls[next];
            _focused.Select();
            Announce();
        }

        internal void Adjust(int direction)
        {
            if (!IsUsable(_focused)) return;
            var slider = _focused.TryCast<Slider>();
            if (slider != null)
            {
                float step = slider.wholeNumbers ? 1f : (slider.maxValue - slider.minValue) * 0.05f;
                slider.value = Mathf.Clamp(slider.value + direction * step, slider.minValue, slider.maxValue);
            }
            else if (TryGetSettingButtons(_focused, out var previous, out var next))
            {
                var button = direction < 0 ? previous : next;
                if (IsUsable(button)) button.onClick.Invoke();
            }
            else if (_focused.TryCast<Toggle>() is { } toggle && toggle != null)
                toggle.isOn = direction > 0;
        }

        internal void Activate()
        {
            if (!IsUsable(_focused)) return;
            if (TryGetSettingButtons(_focused, out _, out _))
            {
                PrismSpeechProvider.Speak("Use Left and Right to change this setting.", true);
                return;
            }
            if (_focused.TryCast<Button>() is { } button && button != null) button.onClick.Invoke();
            else if (_focused.TryCast<Toggle>() is { } toggle && toggle != null) toggle.isOn = !toggle.isOn;
            else if (_focused.TryCast<Slider>() != null)
                PrismSpeechProvider.Speak("Use Left and Right to adjust this slider.", true);
        }

        private void Announce(string title = null)
        {
            string description = MenuDescriber.DescribeElement(_focused.gameObject);
            if (_panel == _settings.resetConfirmationCanvas && _focused.TryCast<Button>() != null)
                description = IsResetCancelButton(_focused) ? "No, keep save files, button" : "Yes, reset save files, button";
            else if (TryGetSettingButtons(_focused, out _, out _))
            {
                var valueText = _focused.transform.parent.GetComponent<Text>();
                var row = _focused.transform.parent.parent;
                var heading = row.GetComponent<Text>();
                string label = heading != null && !string.IsNullOrWhiteSpace(heading.text)
                    ? heading.text.Trim() : row.name.Replace(" Text", "");
                description = $"{label}, {valueText.text}. Left or Right to change.";
            }
            if (title == null && description == _lastDescription) return;
            _lastDescription = description;
            PrismSpeechProvider.Speak(string.IsNullOrWhiteSpace(title) ? description : $"{title}. {description}", true);
        }

        private static bool IsRightHalfOfSetting(Selectable control) =>
            TryGetSettingButtons(control, out _, out var next) && control.gameObject == next.gameObject;

        private bool IsResetCancelButton(Selectable control)
        {
            var no = _settings.noText;
            return (no != null && no.transform.IsChildOf(control.transform)) ||
                   control.name.Equals("No", StringComparison.OrdinalIgnoreCase) ||
                   MenuDescriber.GetElementText(control.gameObject).Equals("No", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetSettingButtons(Selectable control, out Button previous, out Button next)
        {
            previous = null;
            next = null;
            var parent = control.transform.parent;
            if (parent == null || parent.parent == null || parent.GetComponent<Text>() == null) return false;
            foreach (var button in parent.GetComponentsInChildren<Button>())
            {
                if (button.name.StartsWith("Left Button", StringComparison.OrdinalIgnoreCase)) previous = button;
                if (button.name.StartsWith("Right Button", StringComparison.OrdinalIgnoreCase)) next = button;
            }
            return previous != null && next != null;
        }

        private static bool IsUsable(Selectable control) =>
            control != null && control.isActiveAndEnabled && control.IsInteractable();

        private static bool IsVisible(GameObject panel) => panel != null && panel.activeInHierarchy;

        internal void Reset()
        {
            if (_eventSystem != null) _eventSystem.sendNavigationEvents = _previousNavigation;
            _eventSystem = null;
            _panel = null;
            _focused = null;
            _returnFocus = null;
            _lastDescription = null;
            _controls.Clear();
        }
    }
}

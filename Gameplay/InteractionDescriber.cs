using Il2Cpp;
using UnityEngine;
using KamlaAccessibility.Speech;
using KamlaAccessibility.Audio;

namespace KamlaAccessibility.Gameplay
{
    /// <summary>
    /// Tracks and announces interactable objects in front of the player (doors, drawers, items, puzzles).
    /// </summary>
    public class InteractionDescriber
    {
        private GameObject _lastTarget = null;
        private string _lastTargetName = string.Empty;
        private float _timeSinceLastAnnouncement = 0f;
        private const float MinReannounceTime = 1.5f;

        public void UpdateTarget(GameObject target, string interactionPrompt = null)
        {
            if (target == null)
            {
                _lastTarget = null;
                _lastTargetName = string.Empty;
                return;
            }

            _timeSinceLastAnnouncement += Time.deltaTime;

            if (target != _lastTarget || (_timeSinceLastAnnouncement > MinReannounceTime && string.IsNullOrEmpty(_lastTargetName)))
            {
                _lastTarget = target;
                _timeSinceLastAnnouncement = 0f;

                string desc = BuildDescription(target, interactionPrompt);
                if (!string.IsNullOrEmpty(desc) && desc != _lastTargetName)
                {
                    _lastTargetName = desc;
                    AudioCueGenerator.PlayNavTick();
                    PrismSpeechProvider.Speak(desc, true);
                }
            }
        }

        private string BuildDescription(GameObject go, string prompt)
        {
            if (go == null) return string.Empty;

            string name = DescribeObject(go);

            if (!string.IsNullOrEmpty(prompt))
            {
                return string.IsNullOrEmpty(name) ? prompt : $"{name}. {prompt}";
            }

            return name;
        }

        internal static string DescribeObject(GameObject target)
        {
            if (target == null) return null;
            if (target.GetComponentInParent<ItemPicker>() is { } item && item != null)
                return HotspotManager.CleanItemName(item);
            if (target.GetComponentInParent<Key>() is { } key && key != null)
                return HotspotManager.CleanKeyName(key);
            if (target.GetComponentInParent<TPScript>() is { } cover && cover != null)
                return HidingController.GetName(cover);
            if (target.GetComponentInParent<Note>() is { } note && note != null)
                return HotspotManager.CleanNoteName(note);
            if (target.GetComponentInParent<Book>() != null) return "Key book";
            if (target.GetComponentInParent<ItemDetectForWallSlide>() != null) return "Sliding wall mechanism";
            if (target.GetComponentInParent<DoorLocked>() is { } locked && locked != null)
                return HotspotManager.CleanDoorName(locked.gameObject);
            if (target.GetComponentInParent<DoorScriptCode>() is { } scriptDoor && scriptDoor != null)
                return HotspotManager.CleanDoorName(scriptDoor.gameObject);
            if (target.GetComponentInParent<IntroEventDoor>() is { } storyboardDoor && storyboardDoor != null)
                return HotspotManager.CleanDoorName(storyboardDoor.gameObject);
            if (target.GetComponentInParent<Door>() is { } door && door != null)
                return HotspotManager.CleanDoorName(door.gameObject);
            if (target.GetComponentInParent<MainGateOuterArea>() != null) return "Main Haveli Gate";
            if (target.GetComponentInParent<GlassShelf>() != null) return "Glass cabinet";
            if (target.GetComponentInParent<Suitcase>() != null) return "Combination suitcase";
            if (target.GetComponentInParent<Handpump>() != null) return "Handpump";
            if (target.GetComponentInParent<FireBox>() != null) return "Fire";
            if (target.GetComponentInParent<ItemInCourtyard>() is { } offering && offering != null)
                return $"{HotspotManager.FormatRecipeItem(offering.MyItem)} ritual circle";
            if (target.GetComponentInParent<CourtYardMechanic>() != null) return "Courtyard ritual altar";
            if (target.GetComponentInParent<GaneshMurtiManager>() != null) return "Ganesh altar";
            if (target.GetComponentInParent<PushableBrick>() is { } brick && brick != null)
                return PuzzleAccessibility.DescribeBrick(brick);
            var lookable = target.GetComponentInParent<Lookable>();
            if (lookable == null) return null;
            string name = lookable.gameObject.name.Replace("(Clone)", "").Replace("_", " ").Trim();
            return HotspotManager.FormatKnownItemName(name) ?? HotspotManager.SplitCamelCase(name);
        }
    }
}

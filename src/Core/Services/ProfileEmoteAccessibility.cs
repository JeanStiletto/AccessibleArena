using System;
using System.Reflection;

namespace AccessibleArena.Core.Services
{
    internal static class ProfileEmoteAccessibility
    {
        private const string EquippedFieldName = "_isEquipped";
        private const string EmoteButtonFieldName = "_emoteButton";

        internal static bool? TryGetEquippedState(object emoteView)
        {
            if (emoteView == null) return null;

            try
            {
                var field = emoteView.GetType().GetField(
                    EquippedFieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (field == null || field.FieldType != typeof(bool))
                    return null;

                return (bool)field.GetValue(emoteView);
            }
            catch
            {
                return null;
            }
        }

        internal static bool TryClick(object emoteView)
        {
            if (emoteView == null) return false;

            try
            {
                var buttonField = emoteView.GetType().GetField(
                    EmoteButtonFieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var button = buttonField?.GetValue(emoteView);
                if (button == null) return false;

                var clickMethod = button.GetType().GetMethod(
                    "Click",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    Type.EmptyTypes,
                    null);
                if (clickMethod == null) return false;

                clickMethod.Invoke(button, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static bool CanToggle(object emoteView)
        {
            if (emoteView == null) return false;

            try
            {
                var clickSubscribers = emoteView.GetType().GetField(
                    "OnClick",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var clickDelegate = clickSubscribers?.GetValue(emoteView) as Delegate;
                return clickDelegate != null && clickDelegate.GetInvocationList().Length > 0;
            }
            catch
            {
                return false;
            }
        }

        internal static bool? TrySave(object emoteView)
        {
            if (emoteView == null) return null;

            try
            {
                var clickSubscribers = emoteView.GetType().GetField(
                    "OnClick",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var clickDelegate = clickSubscribers?.GetValue(emoteView) as Delegate;
                if (clickDelegate == null) return null;

                foreach (var subscriber in clickDelegate.GetInvocationList())
                {
                    var target = subscriber.Target;
                    if (target == null) continue;

                    var saveMethod = target.GetType().GetMethod(
                        "Save",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null,
                        new[] { typeof(Action) },
                        null);
                    if (saveMethod == null || saveMethod.ReturnType != typeof(bool))
                        continue;

                    return (bool)saveMethod.Invoke(target, new object[] { null });
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}

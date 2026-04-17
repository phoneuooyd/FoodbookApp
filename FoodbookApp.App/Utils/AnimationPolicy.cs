using System;

namespace Foodbook.Utils
{
    internal static class AnimationPolicy
    {
        public const uint PageEnterDurationMs = 95;
        public const double PageEnterOffsetY = 4;

        public const uint TabSwapDurationMs = 130;
        public const double TabSwapOffsetX = 12;
        public const uint TabPostTransitionLoadDelayMs = 24;

        public const uint EntranceDurationMs = 120;
        public const uint RefreshDurationMs = 90;
        public const uint EmphasisDurationMs = 90;
        public const uint DropZoneDurationMs = 80;

        public const uint KeyboardLiftDurationMs = 180;
        public const uint KeyboardResetDurationMs = 140;

        public const uint PopupDimDurationMs = 180;
        public const uint PopupSheetFadeDurationMs = 200;
        public const uint PopupSheetSlideDurationMs = 240;

        public const uint FabToggleDurationMs = 150;

        public static bool IsReduceMotionEnabled()
        {
            try
            {
#if IOS || MACCATALYST
                return UIKit.UIAccessibility.IsReduceMotionEnabled;
#elif ANDROID
                var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                var resolver = activity?.ContentResolver;
                if (resolver == null)
                {
                    return false;
                }

                var scale = Android.Provider.Settings.Global.GetFloat(
                    resolver,
                    Android.Provider.Settings.Global.AnimatorDurationScale,
                    1f);

                return scale <= 0f;
#elif WINDOWS
                return false;
#else
                return false;
#endif
            }
            catch
            {
                return false;
            }
        }

        public static uint ResolveDuration(uint duration)
            => IsReduceMotionEnabled() ? 0u : duration;

        public static double ResolveOffset(double offset)
            => IsReduceMotionEnabled() ? 0d : offset;
    }
}
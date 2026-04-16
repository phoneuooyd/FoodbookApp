using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace Foodbook.Utils
{
    internal static class ComponentAnimationHelper
    {
        private const uint EntranceDurationMs = 120;
        private const uint RefreshDurationMs = 90;
        private const uint EmphasisDurationMs = 90;
        private const uint DropZoneDurationMs = 80;

        public static async Task AnimateEntranceAsync(
            VisualElement? element,
            uint delayMs = 0,
            double offsetY = 8,
            uint durationMs = EntranceDurationMs)
        {
            if (element == null)
                return;

            if (delayMs > 0)
                await Task.Delay((int)delayMs).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    element.AbortAnimation(nameof(AnimateEntranceAsync));
                    element.Opacity = 0;
                    element.TranslationY = offsetY;

                    await Task.WhenAll(
                        element.FadeTo(1, durationMs, Easing.CubicOut),
                        element.TranslateTo(0, 0, durationMs, Easing.CubicOut));
                }
                catch
                {
                    element.Opacity = 1;
                    element.TranslationY = 0;
                }
            });
        }

        public static async Task AnimateSoftRefreshAsync(VisualElement? element)
        {
            if (element == null)
                return;

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    element.AbortAnimation(nameof(AnimateSoftRefreshAsync));
                    await element.FadeTo(0.84, RefreshDurationMs / 2, Easing.CubicIn);
                    await element.FadeTo(1, RefreshDurationMs, Easing.CubicOut);
                }
                catch
                {
                    element.Opacity = 1;
                }
            });
        }

        public static async Task AnimateEmphasisAsync(VisualElement? element, bool emphasized)
        {
            if (element == null)
                return;

            var targetScale = emphasized ? 1.01 : 1;
            var targetOpacity = emphasized ? 0.96 : 1;

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    element.AbortAnimation(nameof(AnimateEmphasisAsync));
                    await Task.WhenAll(
                        element.ScaleTo(targetScale, EmphasisDurationMs, Easing.CubicOut),
                        element.FadeTo(targetOpacity, EmphasisDurationMs, Easing.CubicOut));
                }
                catch
                {
                    element.Scale = 1;
                    element.Opacity = 1;
                }
            });
        }

        public static async Task AnimateDropZoneAsync(VisualElement? element, bool visible)
        {
            if (element == null)
                return;

            var targetOpacity = visible ? 0.6 : 0;

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    element.AbortAnimation(nameof(AnimateDropZoneAsync));
                    await element.FadeTo(targetOpacity, DropZoneDurationMs, Easing.CubicOut);
                }
                catch
                {
                    element.Opacity = targetOpacity;
                }
            });
        }
    }
}

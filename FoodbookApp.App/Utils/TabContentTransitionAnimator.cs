using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace Foodbook.Utils
{
    internal static class TabContentTransitionAnimator
    {
        private const uint ContentSwapDurationMs = 130;
        private const double ContentSwapOffsetX = 12;

        public static async Task AnimateContentSwapAsync(
            ContentView? transitionOverlay,
            View? previousView,
            View? nextView,
            int direction)
        {
            if (nextView == null)
                return;

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var normalizedDirection = direction < 0 ? -1 : 1;
                var enterOffsetX = ContentSwapOffsetX * normalizedDirection;
                var exitOffsetX = -ContentSwapOffsetX * 0.65 * normalizedDirection;

                try
                {
                    nextView.AbortAnimation(nameof(AnimateContentSwapAsync));
                    nextView.Opacity = 0;
                    nextView.TranslationX = enterOffsetX;

                    if (previousView == null || ReferenceEquals(previousView, nextView))
                    {
                        await Task.WhenAll(
                            nextView.FadeTo(1, ContentSwapDurationMs, Easing.CubicOut),
                            nextView.TranslateTo(0, 0, ContentSwapDurationMs, Easing.CubicOut));
                        return;
                    }

                    previousView.AbortAnimation(nameof(AnimateContentSwapAsync));
                    previousView.Opacity = 1;
                    previousView.TranslationX = 0;

                    await Task.WhenAll(
                        nextView.FadeTo(1, ContentSwapDurationMs, Easing.CubicOut),
                        nextView.TranslateTo(0, 0, ContentSwapDurationMs, Easing.CubicOut),
                        previousView.FadeTo(0, ContentSwapDurationMs, Easing.CubicIn),
                        previousView.TranslateTo(exitOffsetX, 0, ContentSwapDurationMs, Easing.CubicIn));
                }
                catch
                {
                    nextView.Opacity = 1;
                    nextView.TranslationX = 0;
                }
                finally
                {
                    if (previousView != null && !ReferenceEquals(previousView, nextView))
                    {
                        previousView.Opacity = 1;
                        previousView.TranslationX = 0;
                    }

                    if (transitionOverlay != null)
                    {
                        transitionOverlay.Content = null;
                        transitionOverlay.IsVisible = false;
                    }
                }
            });
        }
    }
}

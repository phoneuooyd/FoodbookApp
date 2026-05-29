using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace Foodbook.Utils
{
    internal static class ComponentAnimationHelper
    {
        private static readonly SemaphoreSlim PageTransitionGate = new(1, 1);

        public static bool IsReduceMotionEnabled()
            => AnimationPolicy.IsReduceMotionEnabled();

        public static async Task DelayForPostTransitionLoadAsync(CancellationToken cancellationToken)
        {
            var delayMs = AnimationPolicy.ResolveDuration(AnimationPolicy.TabPostTransitionLoadDelayMs);
            if (delayMs == 0)
            {
                return;
            }

            await Task.Delay((int)delayMs, cancellationToken).ConfigureAwait(false);
        }

        public static async Task AnimatePageTransitionAsync(Page? page, bool useVerticalLift = true)
        {
            if (page is not ContentPage contentPage || contentPage.Content is not VisualElement root)
            {
                return;
            }

            await PageTransitionGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var durationMs = AnimationPolicy.ResolveDuration(AnimationPolicy.PageEnterDurationMs);
                    var offsetY = useVerticalLift
                        ? AnimationPolicy.ResolveOffset(AnimationPolicy.PageEnterOffsetY)
                        : 0;

                    try
                    {
                        root.AbortAnimation(nameof(AnimatePageTransitionAsync));

                        if (durationMs == 0)
                        {
                            root.Opacity = 1;
                            root.TranslationY = 0;
                            return;
                        }

                        root.Opacity = 0;
                        root.TranslationY = offsetY;

                        if (useVerticalLift && offsetY > 0)
                        {
                            await Task.WhenAll(
                                root.FadeTo(1, durationMs, Easing.CubicOut),
                                root.TranslateTo(0, 0, durationMs, Easing.CubicOut));
                        }
                        else
                        {
                            await root.FadeTo(1, durationMs, Easing.CubicOut);
                        }
                    }
                    catch
                    {
                        root.Opacity = 1;
                        root.TranslationY = 0;
                    }
                });
            }
            finally
            {
                PageTransitionGate.Release();
            }
        }

        public static async Task AnimateTabTransitionAsync(
            ContentView? transitionOverlay,
            View? previousView,
            View? nextView,
            int direction)
        {
            if (nextView == null)
            {
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var durationMs = AnimationPolicy.ResolveDuration(AnimationPolicy.TabSwapDurationMs);
                var offsetX = AnimationPolicy.ResolveOffset(AnimationPolicy.TabSwapOffsetX);

                var normalizedDirection = direction < 0 ? -1 : 1;
                var enterOffsetX = offsetX * normalizedDirection;
                var exitOffsetX = -offsetX * 0.65 * normalizedDirection;

                try
                {
                    nextView.AbortAnimation(nameof(AnimateTabTransitionAsync));

                    if (durationMs == 0)
                    {
                        nextView.Opacity = 1;
                        nextView.TranslationX = 0;
                        return;
                    }

                    nextView.Opacity = 0;
                    nextView.TranslationX = enterOffsetX;

                    if (previousView == null || ReferenceEquals(previousView, nextView))
                    {
                        await Task.WhenAll(
                            nextView.FadeTo(1, durationMs, Easing.CubicOut),
                            nextView.TranslateTo(0, 0, durationMs, Easing.CubicOut));
                        return;
                    }

                    previousView.AbortAnimation(nameof(AnimateTabTransitionAsync));
                    previousView.Opacity = 1;
                    previousView.TranslationX = 0;

                    await Task.WhenAll(
                        nextView.FadeTo(1, durationMs, Easing.CubicOut),
                        nextView.TranslateTo(0, 0, durationMs, Easing.CubicOut),
                        previousView.FadeTo(0, durationMs, Easing.CubicIn),
                        previousView.TranslateTo(exitOffsetX, 0, durationMs, Easing.CubicIn));
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

        public static async Task AnimateEntranceAsync(
            VisualElement? element,
            uint delayMs = 0,
            double offsetY = 8,
            uint durationMs = AnimationPolicy.EntranceDurationMs)
        {
            if (element == null)
                return;

            if (delayMs > 0)
                await Task.Delay((int)delayMs).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var actualDuration = AnimationPolicy.ResolveDuration(durationMs);
                var actualOffset = AnimationPolicy.ResolveOffset(offsetY);

                try
                {
                    element.AbortAnimation(nameof(AnimateEntranceAsync));

                    if (actualDuration == 0)
                    {
                        element.Opacity = 1;
                        element.TranslationY = 0;
                        return;
                    }

                    element.Opacity = 0;
                    element.TranslationY = actualOffset;

                    await Task.WhenAll(
                        element.FadeTo(1, actualDuration, Easing.CubicOut),
                        element.TranslateTo(0, 0, actualDuration, Easing.CubicOut));
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
                var duration = AnimationPolicy.ResolveDuration(AnimationPolicy.RefreshDurationMs);

                try
                {
                    element.AbortAnimation(nameof(AnimateSoftRefreshAsync));

                    if (duration == 0)
                    {
                        element.Opacity = 1;
                        return;
                    }

                    await element.FadeTo(0.84, Math.Max(1, duration / 2), Easing.CubicIn);
                    await element.FadeTo(1, duration, Easing.CubicOut);
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
                var duration = AnimationPolicy.ResolveDuration(AnimationPolicy.EmphasisDurationMs);

                try
                {
                    element.AbortAnimation(nameof(AnimateEmphasisAsync));

                    if (duration == 0)
                    {
                        element.Scale = targetScale;
                        element.Opacity = targetOpacity;
                        return;
                    }

                    await Task.WhenAll(
                        element.ScaleTo(targetScale, duration, Easing.CubicOut),
                        element.FadeTo(targetOpacity, duration, Easing.CubicOut));
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
                var duration = AnimationPolicy.ResolveDuration(AnimationPolicy.DropZoneDurationMs);

                try
                {
                    element.AbortAnimation(nameof(AnimateDropZoneAsync));

                    if (duration == 0)
                    {
                        element.Opacity = targetOpacity;
                        return;
                    }

                    await element.FadeTo(targetOpacity, duration, Easing.CubicOut);
                }
                catch
                {
                    element.Opacity = targetOpacity;
                }
            });
        }

        public static async Task AnimateKeyboardLiftAsync(
            VisualElement? host,
            bool lifted,
            double liftOffset,
            uint? liftDurationMs = null,
            uint? resetDurationMs = null)
        {
            if (host == null)
            {
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var configuredDuration = lifted
                    ? (liftDurationMs ?? AnimationPolicy.KeyboardLiftDurationMs)
                    : (resetDurationMs ?? AnimationPolicy.KeyboardResetDurationMs);

                var duration = AnimationPolicy.ResolveDuration(configuredDuration);
                var targetY = lifted
                    ? -Math.Abs(AnimationPolicy.ResolveOffset(liftOffset))
                    : 0;

                try
                {
                    host.AbortAnimation(nameof(AnimateKeyboardLiftAsync));

                    if (duration == 0)
                    {
                        host.TranslationY = targetY;
                        return;
                    }

                    await host.TranslateTo(0, targetY, duration, Easing.CubicOut);
                }
                catch
                {
                    host.TranslationY = targetY;
                }
            });
        }

        public static async Task AnimatePopupSheetAsync(
            VisualElement? dimView,
            VisualElement? sheetView,
            bool entering)
        {
            if (dimView == null && sheetView == null)
            {
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var dimDuration = AnimationPolicy.ResolveDuration(AnimationPolicy.PopupDimDurationMs);
                var fadeDuration = AnimationPolicy.ResolveDuration(AnimationPolicy.PopupSheetFadeDurationMs);
                var slideDuration = AnimationPolicy.ResolveDuration(AnimationPolicy.PopupSheetSlideDurationMs);
                var hiddenOffset = AnimationPolicy.ResolveOffset(42);

                try
                {
                    if (entering)
                    {
                        if (dimView != null)
                        {
                            dimView.AbortAnimation(nameof(AnimatePopupSheetAsync));
                            dimView.Opacity = dimDuration == 0 ? 1 : 0;
                        }

                        if (sheetView != null)
                        {
                            sheetView.AbortAnimation(nameof(AnimatePopupSheetAsync));
                            sheetView.Opacity = fadeDuration == 0 ? 1 : 0;
                            sheetView.TranslationY = slideDuration == 0 ? 0 : hiddenOffset;
                        }

                        var dimTask = dimView?.FadeTo(1, dimDuration, Easing.CubicOut) ?? Task.CompletedTask;
                        var fadeTask = sheetView?.FadeTo(1, fadeDuration, Easing.CubicOut) ?? Task.CompletedTask;
                        var slideTask = sheetView?.TranslateTo(0, 0, slideDuration, Easing.CubicOut) ?? Task.CompletedTask;

                        await Task.WhenAll(dimTask, fadeTask, slideTask);
                    }
                    else
                    {
                        var dimTask = dimView?.FadeTo(0, dimDuration, Easing.CubicIn) ?? Task.CompletedTask;
                        var fadeTask = sheetView?.FadeTo(0, fadeDuration, Easing.CubicIn) ?? Task.CompletedTask;
                        var slideTask = sheetView?.TranslateTo(0, hiddenOffset, slideDuration, Easing.CubicIn) ?? Task.CompletedTask;

                        await Task.WhenAll(dimTask, fadeTask, slideTask);
                    }
                }
                catch
                {
                    if (dimView != null)
                    {
                        dimView.Opacity = entering ? 1 : 0;
                    }

                    if (sheetView != null)
                    {
                        sheetView.Opacity = entering ? 1 : 0;
                        sheetView.TranslationY = entering ? 0 : hiddenOffset;
                    }
                }
            });
        }

        public static async Task AnimateRotateAndFadeAsync(
            VisualElement? rotateElement,
            double targetRotation,
            VisualElement? fadeElement,
            double targetOpacity,
            uint durationMs,
            Easing? easing = null)
        {
            if (rotateElement == null && fadeElement == null)
            {
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var duration = AnimationPolicy.ResolveDuration(durationMs);
                var appliedEasing = easing ?? Easing.CubicOut;

                try
                {
                    if (rotateElement != null)
                    {
                        rotateElement.AbortAnimation(nameof(AnimateRotateAndFadeAsync));
                    }

                    if (fadeElement != null)
                    {
                        fadeElement.AbortAnimation(nameof(AnimateRotateAndFadeAsync));
                    }

                    if (duration == 0)
                    {
                        if (rotateElement != null)
                        {
                            rotateElement.Rotation = targetRotation;
                        }

                        if (fadeElement != null)
                        {
                            fadeElement.Opacity = targetOpacity;
                        }

                        return;
                    }

                    var rotateTask = rotateElement?.RotateTo(targetRotation, duration, appliedEasing) ?? Task.CompletedTask;
                    var fadeTask = fadeElement?.FadeTo(targetOpacity, duration, appliedEasing) ?? Task.CompletedTask;

                    await Task.WhenAll(rotateTask, fadeTask);
                }
                catch
                {
                    if (rotateElement != null)
                    {
                        rotateElement.Rotation = targetRotation;
                    }

                    if (fadeElement != null)
                    {
                        fadeElement.Opacity = targetOpacity;
                    }
                }
            });
        }
    }
}

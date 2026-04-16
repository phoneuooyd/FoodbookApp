using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace Foodbook.Utils
{
    internal static class PageTransitionAnimator
    {
        private const uint PageEnterDurationMs = 95;
        private const double PageEnterOffsetY = 4;
        private static readonly SemaphoreSlim AnimationGate = new(1, 1);

        public static async Task AnimatePageEnterAsync(Page? page, bool useVerticalLift = true)
        {
            if (page is not ContentPage contentPage || contentPage.Content is not VisualElement root)
                return;

            await AnimationGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    try
                    {
                        root.AbortAnimation(nameof(AnimatePageEnterAsync));
                        root.Opacity = 0;
                        root.TranslationY = useVerticalLift ? PageEnterOffsetY : 0;

                        if (useVerticalLift)
                        {
                            await Task.WhenAll(
                                root.FadeTo(1, PageEnterDurationMs, Easing.CubicOut),
                                root.TranslateTo(0, 0, PageEnterDurationMs, Easing.CubicOut));
                        }
                        else
                        {
                            await root.FadeTo(1, PageEnterDurationMs, Easing.CubicOut);
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
                AnimationGate.Release();
            }
        }
    }
}

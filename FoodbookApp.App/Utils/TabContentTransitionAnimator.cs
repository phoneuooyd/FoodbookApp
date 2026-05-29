using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace Foodbook.Utils
{
    internal static class TabContentTransitionAnimator
    {
        public static async Task AnimateContentSwapAsync(
            ContentView? transitionOverlay,
            View? previousView,
            View? nextView,
            int direction)
        {
            await ComponentAnimationHelper.AnimateTabTransitionAsync(
                transitionOverlay,
                previousView,
                nextView,
                direction);
        }
    }
}

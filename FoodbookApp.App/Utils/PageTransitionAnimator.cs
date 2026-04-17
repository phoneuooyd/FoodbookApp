using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace Foodbook.Utils
{
    internal static class PageTransitionAnimator
    {
        public static async Task AnimatePageEnterAsync(Page? page, bool useVerticalLift = true)
        {
            await ComponentAnimationHelper.AnimatePageTransitionAsync(page, useVerticalLift);
        }
    }
}

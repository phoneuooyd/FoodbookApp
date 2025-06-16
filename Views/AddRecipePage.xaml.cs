using Microsoft.Maui.Controls;
using Foodbook.Services;
using Foodbook.ViewModels;

namespace Foodbook.Views
{
    public partial class AddRecipePage : ContentPage
    {
        public IEnumerable<Foodbook.Models.Unit> Units => Enum.GetValues(typeof(Foodbook.Models.Unit)).Cast<Foodbook.Models.Unit>();

        public AddRecipePage()
        {
            InitializeComponent();
            BindingContext = new AddRecipeViewModel(
                Application.Current.Handler.MauiContext.Services.GetService<IRecipeService>()
            );
        }
    }
}
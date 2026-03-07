using Foodbook.ViewModels;
using FoodbookApp.Interfaces;

namespace Foodbook.Views;

public partial class FoodbookTemplateFormPage : ContentPage
{
    public FoodbookTemplateFormPage()
    {
        InitializeComponent();

        if (BindingContext is null && FoodbookApp.MauiProgram.ServiceProvider is not null)
        {
            var service = FoodbookApp.MauiProgram.ServiceProvider.GetService(typeof(IFoodbookTemplateService)) as IFoodbookTemplateService;
            if (service is not null)
            {
                BindingContext = new FoodbookTemplateFormViewModel(service);
            }
        }
    }
}

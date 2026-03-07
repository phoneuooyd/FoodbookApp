using Foodbook.Models;
using Foodbook.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Foodbook.Views;

public partial class FoodbookTemplateFormPage : ContentPage
{
    public FoodbookTemplateFormPage()
    {
        InitializeComponent();
        BindingContext = FoodbookApp.MauiProgram.ServiceProvider?.GetRequiredService<FoodbookTemplateFormViewModel>();
    }

    public FoodbookTemplateFormPage(Guid templateId) : this()
    {
        _ = InitializeForEditAsync(templateId);
    }

    public FoodbookTemplateFormPage(string suggestedName, string? suggestedDescription, IReadOnlyCollection<TemplateMeal> meals, int mealsPerDay) : this()
    {
        _ = InitializeForCreateAsync(suggestedName, suggestedDescription, meals, mealsPerDay);
    }

    private async Task InitializeForEditAsync(Guid templateId)
    {
        if (BindingContext is FoodbookTemplateFormViewModel vm)
        {
            await vm.InitializeForEditAsync(templateId);
        }
    }

    private async Task InitializeForCreateAsync(string? suggestedName, string? suggestedDescription, IReadOnlyCollection<TemplateMeal> meals, int mealsPerDay)
    {
        if (BindingContext is FoodbookTemplateFormViewModel vm)
        {
            await vm.InitializeForCreateAsync(suggestedName, suggestedDescription, meals, mealsPerDay);
        }
    }
}

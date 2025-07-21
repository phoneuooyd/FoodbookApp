using Microsoft.Maui.Controls;
using System;
using FoodbookApp;

namespace Foodbook.Services;

[ContentProperty(nameof(Key))]
public class TranslateExtension : IMarkupExtension<BindingBase>
{
    public string Key { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;

    public BindingBase ProvideValue(IServiceProvider serviceProvider)
    {
        var manager = MauiProgram.ServiceProvider?.GetService(typeof(LocalizationResourceManager)) as LocalizationResourceManager;
        return new Binding($"[{Resource}:{Key}]", source: manager ?? new object());
    }

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}

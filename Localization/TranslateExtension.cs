using Microsoft.Maui.Controls;
using System;

namespace Foodbook.Localization;

[ContentProperty(nameof(Text))]
public class TranslateExtension : IMarkupExtension<BindingBase>
{
    public string Text { get; set; } = string.Empty;

    public BindingBase ProvideValue(IServiceProvider serviceProvider)
    {
        return new Binding($"[{Text}]", source: LocalizationResourceManager.Instance);
    }

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider)
        => ProvideValue(serviceProvider);
}

using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;

namespace Foodbook.Services;

[ContentProperty(nameof(Key))]
public class TranslateExtension : IMarkupExtension<BindingBase>
{
    public string BaseName { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;

    public BindingBase ProvideValue(IServiceProvider serviceProvider)
    {
        return new Binding($"[{BaseName}.{Key}]", source: LocalizationResourceManager.Instance);
    }

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider)
        => ProvideValue(serviceProvider);
}

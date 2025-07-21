using System.ComponentModel;
using Microsoft.Maui.Controls;
using Foodbook.Services.Localization;

namespace Foodbook.MarkupExtensions;

[ContentProperty(nameof(Key))]
public class TranslateExtension : IMarkupExtension<BindingBase>
{
    public string Key { get; set; } = string.Empty;
    public string BaseName { get; set; } = string.Empty;

    public BindingBase ProvideValue(IServiceProvider serviceProvider)
    {
        return new Binding
        {
            Mode = BindingMode.OneWay,
            Path = $"[{Key}]",
            Source = new TranslationProxy(BaseName)
        };
    }

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);

    private class TranslationProxy : INotifyPropertyChanged
    {
        private readonly LocalizationResourceManager _manager;
        private readonly string _baseName;

        public TranslationProxy(string baseName)
        {
            _baseName = baseName;
            _manager = MauiProgram.ServiceProvider!.GetRequiredService<LocalizationResourceManager>();
            _manager.PropertyChanged += (s, e) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Value"));
        }

        public string this[string key] => _manager.GetValue(_baseName, key);

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}

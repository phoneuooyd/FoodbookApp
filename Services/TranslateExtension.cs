using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using System;
using FoodbookApp;

namespace Foodbook.Services;

[ContentProperty(nameof(Key))]
public class TranslateExtension : BindableObject, IMarkupExtension<BindingBase>
{
    public static readonly BindableProperty KeyProperty = BindableProperty.Create(nameof(Key), typeof(string), typeof(TranslateExtension), null, propertyChanged: (b,o,n) => ((TranslateExtension)b).OnTranslatedValueChanged());
    public static readonly BindableProperty ResourceProperty = BindableProperty.Create(nameof(Resource), typeof(string), typeof(TranslateExtension), null, propertyChanged: (b,o,n) => ((TranslateExtension)b).OnTranslatedValueChanged());
    public static readonly BindableProperty X0Property = BindableProperty.Create(nameof(X0), typeof(object), typeof(TranslateExtension), null, propertyChanged: (b,o,n) => ((TranslateExtension)b).OnTranslatedValueChanged());
    public static readonly BindableProperty X1Property = BindableProperty.Create(nameof(X1), typeof(object), typeof(TranslateExtension), null, propertyChanged: (b,o,n) => ((TranslateExtension)b).OnTranslatedValueChanged());

    public string Key
    {
        get => (string)GetValue(KeyProperty);
        set => SetValue(KeyProperty, value);
    }

    public string Resource
    {
        get => (string)GetValue(ResourceProperty);
        set => SetValue(ResourceProperty, value);
    }

    public object X0
    {
        get => GetValue(X0Property);
        set => SetValue(X0Property, value);
    }

    public object X1
    {
        get => GetValue(X1Property);
        set => SetValue(X1Property, value);
    }

    private readonly LocalizationResourceManager _manager;

    public TranslateExtension()
    {
        _manager = MauiProgram.ServiceProvider?.GetService(typeof(LocalizationResourceManager)) as LocalizationResourceManager ?? throw new InvalidOperationException("LocalizationResourceManager not available");
        _manager.PropertyChanged += (_, _) => OnTranslatedValueChanged();
    }

    public string? TranslatedValue =>
        _manager[$"{Resource}:{Key}"] is string text
            ? string.Format(text, X0, X1)
            : null;

    private void OnTranslatedValueChanged() => OnPropertyChanged(nameof(TranslatedValue));

    public BindingBase ProvideValue(IServiceProvider serviceProvider)
        => BindingBase.Create<TranslateExtension, string?>(static source => source.TranslatedValue,
            mode: BindingMode.OneWay, source: this);

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}

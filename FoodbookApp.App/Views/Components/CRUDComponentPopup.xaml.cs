using System;
using System.Linq;
using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Views;
using Foodbook.Models;
using Foodbook.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using MauiAppTheme = Microsoft.Maui.ApplicationModel.AppTheme;

namespace Foodbook.Views.Components;

internal sealed class ColorOption
{
    public string Name { get; set; } = string.Empty;
    public string Hex { get; set; } = string.Empty;
    public Color MauiColor => Color.FromArgb(Hex);
}

public class CRUDComponentPopup : Popup
{
    private readonly SettingsViewModel _vm;
    private readonly ObservableCollection<ColorOption> _colorOptions = new(
        new[]
        {
            new ColorOption{ Name="Czerwony", Hex="#E53935"},
            new ColorOption{ Name="Ró¿owy", Hex="#D81B60"},
            new ColorOption{ Name="Fioletowy", Hex="#8E24AA"},
            new ColorOption{ Name="Indygo", Hex="#3949AB"},
            new ColorOption{ Name="Niebieski", Hex="#1E88E5"},
            new ColorOption{ Name="Jasny Niebieski", Hex="#039BE5"},
            new ColorOption{ Name="Cyjan", Hex="#00ACC1"},
            new ColorOption{ Name="Turkus", Hex="#00897B"},
            new ColorOption{ Name="Zielony", Hex="#43A047"},
            new ColorOption{ Name="Jasny Zielony", Hex="#7CB342"},
            new ColorOption{ Name="Limonka", Hex="#C0CA33"},
            new ColorOption{ Name="¯ó³ty", Hex="#FBC02D"},
            new ColorOption{ Name="Pomarañczowy", Hex="#FB8C00"},
            new ColorOption{ Name="G³êboki Pomarañcz", Hex="#F4511E"},
            new ColorOption{ Name="Br¹zowy", Hex="#6D4C41"},
            new ColorOption{ Name="Szary", Hex="#757575"},
        });

    private RecipeLabel? _editing;

    // UI references
    private VerticalStackLayout _listHost = null!;
    private Entry _nameEntry = null!;
    private Picker _colorPicker = null!;
    private Button _deleteButton = null!;
    private Button _saveButton = null!;
    private Border _detailsPanel = null!;
    private Grid _footerBar = null!;

    public CRUDComponentPopup(SettingsViewModel vm)
    {
        _vm = vm;
        CanBeDismissedByTappingOutsideOfPopup = true;
        Content = BuildRoot();
        BuildList();
    }

    private View BuildRoot()
    {
        double popupWidth = Math.Min(DeviceDisplay.Current.MainDisplayInfo.Width / DeviceDisplay.Current.MainDisplayInfo.Density * 0.9, 520);

        _listHost = new VerticalStackLayout { Spacing = 6 };
        _nameEntry = new Entry { Placeholder = "Nazwa etykiety" };
        _colorPicker = new Picker { Title = "Kolor" };
        _colorPicker.ItemsSource = _colorOptions;
        _colorPicker.ItemDisplayBinding = new Binding(nameof(ColorOption.Name));

        _deleteButton = new Button
        {
            Text = "Usuñ",
            BackgroundColor = Color.FromArgb("#C62D3A"),
            TextColor = Colors.White,
            CornerRadius = 8,
            IsVisible = false,
            HeightRequest = 42
        };
        _saveButton = new Button
        {
            Text = "Zapisz",
            CornerRadius = 8,
            HeightRequest = 42
        };
        _saveButton.SetDynamicResource(Button.BackgroundColorProperty, "Primary");
        _saveButton.SetDynamicResource(Button.TextColorProperty, "ButtonPrimaryText");
        var cancelButton = new Button
        {
            Text = "Anuluj",
            CornerRadius = 8,
            HeightRequest = 42
        };
        cancelButton.SetDynamicResource(Button.BackgroundColorProperty, "Secondary");
        cancelButton.TextColor = Colors.White;

        var addButton = new Button { Text = "+", WidthRequest = 44, HeightRequest = 44, CornerRadius = 22 };
        addButton.SetDynamicResource(Button.BackgroundColorProperty, "Primary");
        addButton.SetDynamicResource(Button.TextColorProperty, "ButtonPrimaryText");

        addButton.Clicked += OnAddClicked;
        _saveButton.Clicked += OnSaveClicked;
        _deleteButton.Clicked += OnDeleteClicked;
        cancelButton.Clicked += OnCancelClicked;

        _detailsPanel = new Border
        {
            IsVisible = false,
            Padding = 12,
            StrokeThickness = 1,
            Stroke = new SolidColorBrush(Color.FromArgb("#2D2D2D")),
            StrokeShape = new RoundRectangle { CornerRadius = 12 }
        };
        _detailsPanel.SetDynamicResource(Border.BackgroundColorProperty, "ShellBackgroundColor");

        var gridDetails = new Grid
        {
            RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12,
            RowSpacing = 8
        };
        gridDetails.Add(new Label { Text = "Nazwa", FontSize = 12 }, 0, 0); Grid.SetColumnSpan(_nameEntry, 2); gridDetails.Add(_nameEntry, 0, 1);
        gridDetails.Add(new Label { Text = "Kolor", FontSize = 12, Margin = new Thickness(0, 4, 0, 0) }, 0, 2);
        gridDetails.Add(_colorPicker, 0, 3); Grid.SetColumnSpan(_colorPicker, 2);
        _detailsPanel.Content = gridDetails;

        _footerBar = new Grid
        {
            IsVisible = false,
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            Padding = new Thickness(0,12,0,0)
        };
        _footerBar.Add(new Label { Text = " " }, 0, 0);
        _footerBar.Add(_deleteButton, 1, 0);
        _footerBar.Add(cancelButton, 2, 0);
        _footerBar.Add(_saveButton, 3, 0);

        // Toolbar now only shows add button aligned right
        var toolbar = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            Padding = 0,
            Margin = new Thickness(0,0,0,4)
        };
        toolbar.Add(new Label { Text = " ", IsVisible = false },0,0); // spacer
        toolbar.Add(addButton,1,0);

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            Padding = new Thickness(16,14),
            Margin = new Thickness(0,0,0,8)
        };
        header.SetDynamicResource(BackgroundColorProperty, "ShellBackgroundColor");
        var title = new Label { Text = "Etykiety", FontSize = 20, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, VerticalOptions = LayoutOptions.Center };
        title.SetDynamicResource(Label.TextColorProperty, "ShellTitleColor");
        var closeBtn = new Button { Text = "?", WidthRequest = 36, HeightRequest = 36, CornerRadius = 18, BackgroundColor = Colors.Transparent };
        closeBtn.SetDynamicResource(Button.TextColorProperty, "ShellTitleColor");
        closeBtn.Clicked += async (_,__) => await CloseAsync();
        header.Add(title,0,0); header.Add(closeBtn,1,0);

        var body = new ScrollView { Content = _listHost, Padding = new Thickness(0,0,0,4) };

        var mainStack = new VerticalStackLayout
        {
            Spacing = 8,
            Children = { header, toolbar, body, _detailsPanel, _footerBar }
        };

        var outerBorder = new Border
        {
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Padding = 0,
            WidthRequest = popupWidth,
            MaximumWidthRequest = 520,
            Content = mainStack
        };
        outerBorder.SetDynamicResource(Border.BackgroundColorProperty, "PageBackgroundColor");
        outerBorder.SetDynamicResource(Border.StrokeProperty, "Secondary");
        return outerBorder;
    }

    private void BuildList()
    {
        _listHost.Children.Clear();
        foreach (var lbl in _vm.Labels)
        {
            var itemBorder = BuildLabelItem(lbl);
            _listHost.Children.Add(itemBorder);
        }
    }

    private View BuildLabelItem(RecipeLabel lbl)
    {
        var isDark = Application.Current?.RequestedTheme == MauiAppTheme.Dark;
        var border = new Border
        {
            StrokeThickness = 1,
            Padding = new Thickness(10,6),
            StrokeShape = new RoundRectangle { CornerRadius = 10 }
        };
        border.SetDynamicResource(Border.StrokeProperty, "Secondary");
        border.SetDynamicResource(Border.BackgroundColorProperty, "ShellBackgroundColor");

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };

        var colorDot = new Frame
        {
            WidthRequest = 16,
            HeightRequest = 16,
            CornerRadius = 8,
            Padding = 0,
            HasShadow = false,
            BackgroundColor = Color.FromArgb(lbl.ColorHex ?? "#757575")
        };
        grid.Add(colorDot,0,0);

        var nameLabel = new Label { Text = lbl.Name, VerticalOptions = LayoutOptions.Center };
        nameLabel.SetDynamicResource(Label.TextColorProperty, "PrimaryText");
        grid.Add(nameLabel,1,0);

        var deleteBtn = new Button
        {
            Text = "?",
            WidthRequest = 36,
            HeightRequest = 36,
            CornerRadius = 18,
            BackgroundColor = Colors.Transparent,
            Padding = 0,
            FontAttributes = FontAttributes.Bold,
            Command = new Command(() => DeleteLabel(lbl))
        };
        deleteBtn.SetDynamicResource(Button.TextColorProperty, "ShellTitleColor");
        grid.Add(deleteBtn,2,0);

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_,__) => StartEdit(lbl);
        grid.GestureRecognizers.Add(tap);

        border.Content = grid;
        return border;
    }

    private void OnAddClicked(object? sender, EventArgs e)
    {
        _editing = null;
        _nameEntry.Text = string.Empty;
        _colorPicker.SelectedIndex = -1;
        _deleteButton.IsVisible = false;
        _saveButton.Text = "Dodaj";
        ShowDetailsPanels();
    }

    private void StartEdit(RecipeLabel lbl)
    {
        _editing = lbl;
        _nameEntry.Text = lbl.Name;
        var idx = _colorOptions.IndexOf(_colorOptions.FirstOrDefault(c => string.Equals(c.Hex, lbl.ColorHex, StringComparison.OrdinalIgnoreCase)) ?? _colorOptions.First());
        _colorPicker.SelectedIndex = idx;
        _deleteButton.IsVisible = true;
        _saveButton.Text = "Zapisz";
        ShowDetailsPanels();
    }

    private void DeleteLabel(RecipeLabel lbl)
    {
        _vm.SelectedLabel = lbl;
        if (_vm.DeleteLabelCommand.CanExecute(null))
            _vm.DeleteLabelCommand.Execute(null);
        if (_editing == lbl) HideDetailsPanels();
        BuildList();
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        var name = _nameEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;
        var selected = _colorPicker.SelectedItem as ColorOption;
        var colorHex = selected?.Hex ?? "#757575";
        if (_editing == null)
        {
            _vm.NewLabelName = name;
            _vm.NewLabelColor = colorHex;
            if (_vm.AddLabelCommand.CanExecute(null)) _vm.AddLabelCommand.Execute(null);
        }
        else
        {
            _vm.SelectedLabel = _editing;
            _vm.EditLabelName = name;
            _vm.EditLabelColor = colorHex;
            if (_vm.UpdateLabelCommand.CanExecute(null)) _vm.UpdateLabelCommand.Execute(null);
        }
        HideDetailsPanels();
        BuildList();
    }

    private void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_editing == null) return;
        DeleteLabel(_editing);
    }

    private void OnCancelClicked(object? sender, EventArgs e)
    {
        HideDetailsPanels();
    }

    private void ShowDetailsPanels()
    {
        _detailsPanel.IsVisible = true;
        _footerBar.IsVisible = true;
    }

    private void HideDetailsPanels()
    {
        _detailsPanel.IsVisible = false;
        _footerBar.IsVisible = false;
        _editing = null;
    }
}

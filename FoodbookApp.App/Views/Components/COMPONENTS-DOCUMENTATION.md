# Reusable Components Documentation

Dokumentacja ponownie uøywalnych komponentÛw UI dla aplikacji FoodbookApp, zgodnie z zasadπ DRY (Don't Repeat Yourself).

## Przeglπd KomponentÛw

W odpowiedzi na duplikacjÍ kodu pomiÍdzy stronami `RecipesPage.xaml` i `IngredientsPage.xaml`, zosta≥y utworzone nastÍpujπce komponenty reuøywalne:

### 1. ModernSearchBarComponent
**Lokalizacja**: `Views/Components/ModernSearchBarComponent.xaml`

**Cel**: Nowoczesny pasek wyszukiwania z ikonπ, polem tekstowym i przyciskiem czyszczenia.

**W≥aúciwoúci Bindable**:
- `SearchText` (string, TwoWay) - Tekst wyszukiwania
- `PlaceholderText` (string) - Tekst placeholder
- `ClearCommand` (ICommand) - Komenda czyszczenia tekstu
- `IsVisible` (bool) - WidocznoúÊ komponentu

**Przyk≥ad uøycia**:
```xaml
<components:ModernSearchBarComponent 
    SearchText="{Binding SearchText}"
    PlaceholderText="{loc:Translate Resource=RecipesPageResources, Key=SearchPlaceholder}"
    ClearCommand="{Binding ClearSearchCommand}"
    IsVisible="{Binding IsLoading, Converter={StaticResource InvertedBoolConverter}}" />
```

### 2. GenericListComponent
**Lokalizacja**: `Views/Components/GenericListComponent.xaml`

**Cel**: Uniwersalny komponent listy z funkcjonalnoúciπ refresh i konfigurowalnymi stanami pustymi.

**W≥aúciwoúci Bindable**:
- `ItemsSource` (IEnumerable) - èrÛd≥o danych
- `ItemTemplate` (DataTemplate) - Szablon elementu listy
- `IsRefreshing` (bool) - Stan odúwieøania
- `RefreshCommand` (ICommand) - Komenda odúwieøania
- `EmptyTitle` (string) - Tytu≥ pustej listy
- `EmptyHint` (string) - Podpowiedü dla pustej listy
- `IsVisible` (bool) - WidocznoúÊ komponentu

**Przyk≥ad uøycia**:
```xaml
<components:GenericListComponent 
    ItemsSource="{Binding Recipes}"
    ItemTemplate="{StaticResource RecipeItemTemplate}"
    IsRefreshing="{Binding IsRefreshing}"
    RefreshCommand="{Binding RefreshCommand}"
    EmptyTitle="{loc:Translate Resource=RecipesPageResources, Key=EmptyTitle}"
    EmptyHint="{loc:Translate Resource=RecipesPageResources, Key=EmptyHint}"
    IsVisible="{Binding IsLoading, Converter={StaticResource InvertedBoolConverter}}" />
```

### 3. FloatingActionButtonComponent
**Lokalizacja**: `Views/Components/FloatingActionButtonComponent.xaml`

**Cel**: P≥ywajπcy przycisk akcji dla g≥Ûwnych dzia≥aÒ.

**W≥aúciwoúci Bindable**:
- `Command` (ICommand) - Komenda do wykonania
- `ButtonText` (string) - Tekst przycisku (domyúlnie "+")
- `IsVisible` (bool) - WidocznoúÊ komponentu

**Przyk≥ad uøycia**:
```xaml
<components:FloatingActionButtonComponent 
    Command="{Binding AddRecipeCommand}"
    IsVisible="{Binding IsLoading, Converter={StaticResource InvertedBoolConverter}}" />
```

### 4. RecipeListItemComponent
**Lokalizacja**: `Views/Components/RecipeListItemComponent.xaml`

**Cel**: Szablon elementu listy przepisÛw z informacjami odøywczymi i przyciskami akcji.

**W≥aúciwoúci Bindable**:
- `EditCommand` (ICommand) - Komenda edycji
- `DeleteCommand` (ICommand) - Komenda usuniÍcia

**DataContext**: Oczekuje obiektu typu `Recipe`

**Przyk≥ad uøycia**:
```xaml
<DataTemplate x:Key="RecipeItemTemplate" x:DataType="models:Recipe">
    <components:RecipeListItemComponent 
        EditCommand="{Binding BindingContext.EditRecipeCommand, Source={x:Reference ThisPage}}"
        DeleteCommand="{Binding BindingContext.DeleteRecipeCommand, Source={x:Reference ThisPage}}" />
</DataTemplate>
```

### 5. IngredientListItemComponent
**Lokalizacja**: `Views/Components/IngredientListItemComponent.xaml`

**Cel**: Szablon elementu listy sk≥adnikÛw z iloúciπ, jednostkπ, informacjami odøywczymi i przyciskami akcji.

**W≥aúciwoúci Bindable**:
- `EditCommand` (ICommand) - Komenda edycji
- `DeleteCommand` (ICommand) - Komenda usuniÍcia

**DataContext**: Oczekuje obiektu typu `Ingredient`

**Przyk≥ad uøycia**:
```xaml
<DataTemplate x:Key="IngredientItemTemplate" x:DataType="models:Ingredient">
    <components:IngredientListItemComponent 
        EditCommand="{Binding BindingContext.EditCommand, Source={x:Reference ThisPage}}"
        DeleteCommand="{Binding BindingContext.DeleteCommand, Source={x:Reference ThisPage}}" />
</DataTemplate>
```

## Refaktoryzowane Strony

### RecipesPage.xaml
**Przed refaktoryzacjπ**: 105 linii duplikowanego kodu
**Po refaktoryzacji**: 52 linie z uøyciem komponentÛw

**Zmiany**:
- Zastπpiono tradycyjny search bar komponentem `ModernSearchBarComponent`
- Zastπpiono `RefreshView` + `CollectionView` komponentem `GenericListComponent`
- Zastπpiono inline template komponentem `RecipeListItemComponent`
- Zastπpiono tradycyjny FAB komponentem `FloatingActionButtonComponent`

### IngredientsPage.xaml
**Przed refaktoryzacjπ**: 110 linii duplikowanego kodu
**Po refaktoryzacji**: 52 linie z uøyciem komponentÛw

**Zmiany**:
- Identyczne jak w RecipesPage.xaml, z wyjπtkiem uøycia `IngredientListItemComponent`

## Korzyúci z Refaktoryzacji

### 1. ZgodnoúÊ z zasadπ DRY
- Eliminacja duplikacji kodu miÍdzy stronami
- Centralizacja logiki UI w komponentach
- £atwiejsze utrzymanie i aktualizacja

### 2. ModularnoúÊ i ReuøywalnoúÊ
- Komponenty mogπ byÊ uøywane w innych czÍúciach aplikacji
- £atwe dodawanie nowych stron z podobnπ funkcjonalnoúciπ
- SpÛjny wyglπd i zachowanie w ca≥ej aplikacji

### 3. Parametryzacja
- Wszystkie komponenty sπ w pe≥ni konfigurowalne
- Bindable properties umoøliwiajπ elastyczne dostosowanie
- Zachowanie oryginalnej funkcjonalnoúci

### 4. £atwoúÊ Testowania
- Komponenty moøna testowaÊ niezaleønie
- Izolacja logiki UI
- Lepsze pokrycie testami

### 5. Konserwacja Kodu
- Zmiany stylu wymagajπ modyfikacji tylko w jednym miejscu
- £atwiejsze dodawanie nowych funkcji
- Redukcja b≥ÍdÛw zwiπzanych z duplikacjπ

## Wykorzystanie w Przysz≥oúci

Te komponenty mogπ byÊ uøywane w:
- Nowych stronach z podobnπ funkcjonalnoúciπ (np. Shopping Lists, Meal Plans)
- RÛønych kontekstach z zachowaniem spÛjnoúci UI
- Rozszerzeniach funkcjonalnoúci bez duplikacji kodu

## Wzorce Zastosowane

### 1. Composition over Inheritance
- Komponenty sk≥adane razem zamiast dziedziczenia
- WiÍksza elastycznoúÊ i modularnoúÊ

### 2. Bindable Properties Pattern
- Standardowy wzorzec .NET MAUI dla komponentÛw
- Type-safe binding z IntelliSense

### 3. Separation of Concerns
- Kaødy komponent ma jednπ odpowiedzialnoúÊ
- Czysta separacja miÍdzy logikπ a prezentacjπ

### 4. Template Method Pattern
- GenericListComponent jako szablon z konfigurowalnymi czÍúciami
- ItemTemplate pozwala na dostosowanie bez modyfikacji komponentu

## Podsumowanie

Refaktoryzacja zaowocowa≥a:
- **Redukcjπ kodu o ~50%** w kaødej ze stron
- **Eliminacjπ duplikacji** miÍdzy RecipesPage i IngredientsPage  
- **Stworzeniem biblioteki** reuøywalnych komponentÛw UI
- **Zachowaniem pe≥nej funkcjonalnoúci** oryginalnych stron
- **Przygotowaniem fundamentu** dla przysz≥ych rozszerzeÒ

Wszystkie komponenty sπ gotowe do uøycia w innych czÍúciach aplikacji i mogπ byÊ ≥atwo rozszerzane o nowe funkcjonalnoúci.
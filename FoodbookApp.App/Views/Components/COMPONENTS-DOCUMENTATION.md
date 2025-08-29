# Reusable Components Documentation

Dokumentacja ponownie u�ywalnych komponent�w UI dla aplikacji FoodbookApp, zgodnie z zasad� DRY (Don't Repeat Yourself).

## Przegl�d Komponent�w

W odpowiedzi na duplikacj� kodu pomi�dzy stronami `RecipesPage.xaml` i `IngredientsPage.xaml`, zosta�y utworzone nast�puj�ce komponenty reu�ywalne:

### 1. ModernSearchBarComponent
**Lokalizacja**: `Views/Components/ModernSearchBarComponent.xaml`

**Cel**: Nowoczesny pasek wyszukiwania z ikon�, polem tekstowym i przyciskiem czyszczenia.

**W�a�ciwo�ci Bindable**:
- `SearchText` (string, TwoWay) - Tekst wyszukiwania
- `PlaceholderText` (string) - Tekst placeholder
- `ClearCommand` (ICommand) - Komenda czyszczenia tekstu
- `IsVisible` (bool) - Widoczno�� komponentu

**Przyk�ad u�ycia**:
```xaml
<components:ModernSearchBarComponent 
    SearchText="{Binding SearchText}"
    PlaceholderText="{loc:Translate Resource=RecipesPageResources, Key=SearchPlaceholder}"
    ClearCommand="{Binding ClearSearchCommand}"
    IsVisible="{Binding IsLoading, Converter={StaticResource InvertedBoolConverter}}" />
```

### 2. GenericListComponent
**Lokalizacja**: `Views/Components/GenericListComponent.xaml`

**Cel**: Uniwersalny komponent listy z funkcjonalno�ci� refresh i konfigurowalnymi stanami pustymi.

**W�a�ciwo�ci Bindable**:
- `ItemsSource` (IEnumerable) - �r�d�o danych
- `ItemTemplate` (DataTemplate) - Szablon elementu listy
- `IsRefreshing` (bool) - Stan od�wie�ania
- `RefreshCommand` (ICommand) - Komenda od�wie�ania
- `EmptyTitle` (string) - Tytu� pustej listy
- `EmptyHint` (string) - Podpowied� dla pustej listy
- `IsVisible` (bool) - Widoczno�� komponentu

**Przyk�ad u�ycia**:
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

**Cel**: P�ywaj�cy przycisk akcji dla g��wnych dzia�a�.

**W�a�ciwo�ci Bindable**:
- `Command` (ICommand) - Komenda do wykonania
- `ButtonText` (string) - Tekst przycisku (domy�lnie "+")
- `IsVisible` (bool) - Widoczno�� komponentu

**Przyk�ad u�ycia**:
```xaml
<components:FloatingActionButtonComponent 
    Command="{Binding AddRecipeCommand}"
    IsVisible="{Binding IsLoading, Converter={StaticResource InvertedBoolConverter}}" />
```

### 4. RecipeListItemComponent
**Lokalizacja**: `Views/Components/RecipeListItemComponent.xaml`

**Cel**: Szablon elementu listy przepis�w z informacjami od�ywczymi i przyciskami akcji.

**W�a�ciwo�ci Bindable**:
- `EditCommand` (ICommand) - Komenda edycji
- `DeleteCommand` (ICommand) - Komenda usuni�cia

**DataContext**: Oczekuje obiektu typu `Recipe`

**Przyk�ad u�ycia**:
```xaml
<DataTemplate x:Key="RecipeItemTemplate" x:DataType="models:Recipe">
    <components:RecipeListItemComponent 
        EditCommand="{Binding BindingContext.EditRecipeCommand, Source={x:Reference ThisPage}}"
        DeleteCommand="{Binding BindingContext.DeleteRecipeCommand, Source={x:Reference ThisPage}}" />
</DataTemplate>
```

### 5. IngredientListItemComponent
**Lokalizacja**: `Views/Components/IngredientListItemComponent.xaml`

**Cel**: Szablon elementu listy sk�adnik�w z ilo�ci�, jednostk�, informacjami od�ywczymi i przyciskami akcji.

**W�a�ciwo�ci Bindable**:
- `EditCommand` (ICommand) - Komenda edycji
- `DeleteCommand` (ICommand) - Komenda usuni�cia

**DataContext**: Oczekuje obiektu typu `Ingredient`

**Przyk�ad u�ycia**:
```xaml
<DataTemplate x:Key="IngredientItemTemplate" x:DataType="models:Ingredient">
    <components:IngredientListItemComponent 
        EditCommand="{Binding BindingContext.EditCommand, Source={x:Reference ThisPage}}"
        DeleteCommand="{Binding BindingContext.DeleteCommand, Source={x:Reference ThisPage}}" />
</DataTemplate>
```

## Refaktoryzowane Strony

### RecipesPage.xaml
**Przed refaktoryzacj�**: 105 linii duplikowanego kodu
**Po refaktoryzacji**: 52 linie z u�yciem komponent�w

**Zmiany**:
- Zast�piono tradycyjny search bar komponentem `ModernSearchBarComponent`
- Zast�piono `RefreshView` + `CollectionView` komponentem `GenericListComponent`
- Zast�piono inline template komponentem `RecipeListItemComponent`
- Zast�piono tradycyjny FAB komponentem `FloatingActionButtonComponent`

### IngredientsPage.xaml
**Przed refaktoryzacj�**: 110 linii duplikowanego kodu
**Po refaktoryzacji**: 52 linie z u�yciem komponent�w

**Zmiany**:
- Identyczne jak w RecipesPage.xaml, z wyj�tkiem u�ycia `IngredientListItemComponent`

## Korzy�ci z Refaktoryzacji

### 1. Zgodno�� z zasad� DRY
- Eliminacja duplikacji kodu mi�dzy stronami
- Centralizacja logiki UI w komponentach
- �atwiejsze utrzymanie i aktualizacja

### 2. Modularno�� i Reu�ywalno��
- Komponenty mog� by� u�ywane w innych cz�ciach aplikacji
- �atwe dodawanie nowych stron z podobn� funkcjonalno�ci�
- Sp�jny wygl�d i zachowanie w ca�ej aplikacji

### 3. Parametryzacja
- Wszystkie komponenty s� w pe�ni konfigurowalne
- Bindable properties umo�liwiaj� elastyczne dostosowanie
- Zachowanie oryginalnej funkcjonalno�ci

### 4. �atwo�� Testowania
- Komponenty mo�na testowa� niezale�nie
- Izolacja logiki UI
- Lepsze pokrycie testami

### 5. Konserwacja Kodu
- Zmiany stylu wymagaj� modyfikacji tylko w jednym miejscu
- �atwiejsze dodawanie nowych funkcji
- Redukcja b��d�w zwi�zanych z duplikacj�

## Wykorzystanie w Przysz�o�ci

Te komponenty mog� by� u�ywane w:
- Nowych stronach z podobn� funkcjonalno�ci� (np. Shopping Lists, Meal Plans)
- R�nych kontekstach z zachowaniem sp�jno�ci UI
- Rozszerzeniach funkcjonalno�ci bez duplikacji kodu

## Wzorce Zastosowane

### 1. Composition over Inheritance
- Komponenty sk�adane razem zamiast dziedziczenia
- Wi�ksza elastyczno�� i modularno��

### 2. Bindable Properties Pattern
- Standardowy wzorzec .NET MAUI dla komponent�w
- Type-safe binding z IntelliSense

### 3. Separation of Concerns
- Ka�dy komponent ma jedn� odpowiedzialno��
- Czysta separacja mi�dzy logik� a prezentacj�

### 4. Template Method Pattern
- GenericListComponent jako szablon z konfigurowalnymi cz�ciami
- ItemTemplate pozwala na dostosowanie bez modyfikacji komponentu

## Podsumowanie

Refaktoryzacja zaowocowa�a:
- **Redukcj� kodu o ~50%** w ka�dej ze stron
- **Eliminacj� duplikacji** mi�dzy RecipesPage i IngredientsPage  
- **Stworzeniem biblioteki** reu�ywalnych komponent�w UI
- **Zachowaniem pe�nej funkcjonalno�ci** oryginalnych stron
- **Przygotowaniem fundamentu** dla przysz�ych rozszerze�

Wszystkie komponenty s� gotowe do u�ycia w innych cz�ciach aplikacji i mog� by� �atwo rozszerzane o nowe funkcjonalno�ci.
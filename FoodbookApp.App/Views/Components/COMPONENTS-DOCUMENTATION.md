# Reusable Components Documentation

Dokumentacja ponownie uøywalnych komponentÛw UI dla aplikacji FoodbookApp, zgodnie z zasadπ DRY (Don't Repeat Yourself).

## Przeglπd KomponentÛw

W odpowiedzi na duplikacjÍ kodu pomiÍdzy stronami `RecipesPage.xaml`, `IngredientsPage.xaml` i `ShoppingListPage.xaml`, zosta≥y utworzone nastÍpujπce komponenty reuøywalne:

### 1. ModernSearchBarComponent
**Lokalizacja**: `Views/Components/ModernSearchBarComponent.xaml`

**Cel**: Nowoczesny pasek wyszukiwania z ikonπ, polem tekstowym i przyciskiem czyszczenia.

**W≥aúciwoúci Bindable**:
- `SearchText` (string, TwoWay) - Tekst wyszukiwania
- `PlaceholderText` (string) - Tekst placeholder
- `ClearCommand` (ICommand) - Komenda czyszczenia tekstu
- `IsVisible` (bool) - WidocznoúÊ komponentu

**Przyk≥ad uøycia**:<components:ModernSearchBarComponent 
    SearchText="{Binding SearchText}"
    PlaceholderText="{loc:Translate Resource=RecipesPageResources, Key=SearchPlaceholder}"
    ClearCommand="{Binding ClearSearchCommand}"
    IsVisible="{Binding IsLoading, Converter={StaticResource InvertedBoolConverter}}" />
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

**Przyk≥ad uøycia**:<components:GenericListComponent 
    ItemsSource="{Binding Recipes}"
    ItemTemplate="{StaticResource RecipeItemTemplate}"
    IsRefreshing="{Binding IsRefreshing}"
    RefreshCommand="{Binding RefreshCommand}"
    EmptyTitle="{loc:Translate Resource=RecipesPageResources, Key=EmptyTitle}"
    EmptyHint="{loc:Translate Resource=RecipesPageResources, Key=EmptyHint}"
    IsVisible="{Binding IsLoading, Converter={StaticResource InvertedBoolConverter}}" />
### 3. FloatingActionButtonComponent
**Lokalizacja**: `Views/Components/FloatingActionButtonComponent.xaml`

**Cel**: P≥ywajπcy przycisk akcji dla g≥Ûwnych dzia≥aÒ.

**W≥aúciwoúci Bindable**:
- `Command` (ICommand) - Komenda do wykonania
- `ButtonText` (string) - Tekst przycisku (domyúlnie "+")
- `IsVisible` (bool) - WidocznoúÊ komponentu

**Przyk≥ad uøycia**:<components:FloatingActionButtonComponent 
    Command="{Binding AddRecipeCommand}"
    IsVisible="{Binding IsLoading, Converter={StaticResource InvertedBoolConverter}}" />
### 4. UniversalListItemComponent ? **UNIWERSALNY KOMPONENT MULTI-MODE**
**Lokalizacja**: `Views/Components/UniversalListItemComponent.xaml`

**Cel**: Ultra-uniwersalny szablon elementu listy obs≥ugujπcy przepisy, sk≥adniki i plany zakupÛw z pe≥nπ konfigurowalnoúciπ.

**W≥aúciwoúci Bindable**:
- `EditCommand` (ICommand) - Komenda edycji/otwarcia
- `DeleteCommand` (ICommand) - Komenda usuniÍcia
- `ArchiveCommand` (ICommand) - Komenda archiwizacji (dla planÛw)
- `ShowSubtitle` (bool) - Wyúwietla/ukrywa podtytu≥ z iloúciπ i jednostkπ
- `ShowNutritionLayout` (bool) - Wyúwietla/ukrywa informacje odøywcze
- `ShowPlanLayout` (bool) - Wyúwietla/ukrywa informacje o planie (daty)
- `ShowDeleteButton` (bool) - Wyúwietla/ukrywa przycisk usuwania
- `ShowArchiveButton` (bool) - Wyúwietla/ukrywa przycisk archiwizacji

**DataContext**: Obs≥uguje obiekty typu `Recipe`, `Ingredient` lub `Plan`

### Tryby Konfiguracji

#### **TRYB 1: Recipe Mode** (Przepisy)<DataTemplate x:Key="RecipeItemTemplate" x:DataType="models:Recipe">
    <components:UniversalListItemComponent 
        EditCommand="{Binding BindingContext.EditRecipeCommand, Source={x:Reference ThisPage}}"
        DeleteCommand="{Binding BindingContext.DeleteRecipeCommand, Source={x:Reference ThisPage}}"
        ShowSubtitle="False"
        ShowNutritionLayout="True"
        ShowPlanLayout="False"
        ShowDeleteButton="True"
        ShowArchiveButton="False" />
</DataTemplate>
#### **TRYB 2: Ingredient Mode** (Sk≥adniki)<DataTemplate x:Key="IngredientItemTemplate" x:DataType="models:Ingredient">
    <components:UniversalListItemComponent 
        EditCommand="{Binding BindingContext.EditCommand, Source={x:Reference ThisPage}}"
        DeleteCommand="{Binding BindingContext.DeleteCommand, Source={x:Reference ThisPage}}"
        ShowSubtitle="True"
        ShowNutritionLayout="True"
        ShowPlanLayout="False"
        ShowDeleteButton="True"
        ShowArchiveButton="False" />
</DataTemplate>
#### **TRYB 3: Plan Mode** (Plany ZakupÛw) ? **NOWY**<DataTemplate x:Key="PlanItemTemplate" x:DataType="models:Plan">
    <components:UniversalListItemComponent 
        EditCommand="{Binding BindingContext.OpenPlanCommand, Source={x:Reference ThisPage}}"
        ArchiveCommand="{Binding BindingContext.ArchivePlanCommand, Source={x:Reference ThisPage}}"
        ShowNutritionLayout="False"
        ShowPlanLayout="True"
        ShowDeleteButton="False"
        ShowArchiveButton="True"
        ShowSubtitle="False" />
</DataTemplate>
### Struktura UI w rÛønych trybach

| Tryb | Name/Label | Subtitle | Nutrition | Plan Dates | Delete | Archive |
|------|------------|----------|-----------|------------|--------|---------|
| **Recipe** | ? Name | ? | ? Calories, P, F, C | ? | ? | ? |
| **Ingredient** | ? Name | ? Quantity + Unit | ? Calories, P, F, C | ? | ? | ? |
| **Plan** | ? Label | ? | ? | ? StartDate + EndDate | ? | ? |

## Refaktoryzowane Strony

### RecipesPage.xaml
**Przed refaktoryzacjπ**: 105 linii z duplikowanym kodem
**Po trzeciej refaktoryzacji**: 52 linie z uniwersalnym komponentem

**Konfiguracja**:
- `ShowNutritionLayout="True"` - wyúwietla kalorie i makro
- `ShowPlanLayout="False"` - ukrywa informacje o planie
- `ShowDeleteButton="True"` - pokazuje przycisk usuwania
- `ShowArchiveButton="False"` - ukrywa przycisk archiwizacji

### IngredientsPage.xaml
**Przed refaktoryzacjπ**: 110 linii z duplikowanym kodem  
**Po trzeciej refaktoryzacji**: 52 linie z uniwersalnym komponentem

**Konfiguracja**:
- `ShowSubtitle="True"` - wyúwietla iloúÊ i jednostkÍ
- `ShowNutritionLayout="True"` - wyúwietla kalorie i makro
- `ShowDeleteButton="True"` - pokazuje przycisk usuwania

### ShoppingListPage.xaml ? **NOWA REFAKTORYZACJA**
**Przed refaktoryzacjπ**: 60 linii z dedykowanπ strukturπ
**Po refaktoryzacji**: 25 linii z uniwersalnym komponentem

**Zmiany**:
- Zastπpiono dedykowanπ strukturÍ `Frame + Grid + VerticalStackLayout` uniwersalnym komponentem
- Dodano obs≥ugÍ trybu Plan z datami StartDate/EndDate
- Zastπpiono zwyk≥π CollectionView komponentem GenericListComponent
- Zachowano funkcjonalnoúÊ archiwizacji planÛw

**Konfiguracja**:
- `ShowPlanLayout="True"` - wyúwietla daty planu
- `ShowNutritionLayout="False"` - ukrywa informacje odøywcze
- `ShowArchiveButton="True"` - pokazuje przycisk archiwizacji
- `ShowDeleteButton="False"` - ukrywa przycisk usuwania

## Ewolucja Komponentu UniversalListItemComponent

### Faza 1: Podstawowa Unifikacja
- Po≥πczenie `RecipeListItemComponent` + `IngredientListItemComponent`
- 1 parametr konfiguracji: `ShowSubtitle`

### Faza 2: Multi-Mode Extension ? **AKTUALNA**
- Dodanie obs≥ugi `Plan` jako trzeciego typu danych
- 5 nowych parametrÛw konfiguracji:
  - `ShowNutritionLayout` - kontrola wyúwietlania makro
  - `ShowPlanLayout` - kontrola wyúwietlania dat planu
  - `ShowDeleteButton` - kontrola przycisku usuwania
  - `ShowArchiveButton` - kontrola przycisku archiwizacji
  - `ArchiveCommand` - komenda archiwizacji

### Nowe Funkcjonalnoúci

#### 1. Plan Layout Support
- **Label wyúwietlanie** - tytu≥ planu
- **Date Range** - StartDate i EndDate z formatowaniem
- **Stylowanie dat** - kolory Primary z AppTheme binding

#### 2. Flexible Action Buttons
- **Delete Button** - tradycyjny przycisk usuwania (Recipe/Ingredient)
- **Archive Button** - przycisk archiwizacji z ikonπ save.png (Plan)
- **Conditional Visibility** - kaødy przycisk moøe byÊ niezaleønie ukryty/pokazany

#### 3. Layout Switching
- **Nutrition Layout** - horizontalny stack z kaloriami i makro
- **Plan Layout** - wertykalny stack z datami
- **Exclusive modes** - jeden typ layoutu na raz

## Korzyúci z Rozszerzenia

### 1. Maksymalna Unifikacja
- **3 typy danych** w jednym komponencie (Recipe, Ingredient, Plan)
- **Zero duplikacji** miÍdzy wszystkimi stronami list
- **SpÛjna architektura** we wszystkich czÍúciach aplikacji

### 2. Ultimate Flexibility
- **8 parametrÛw konfiguracji** dla pe≥nej kontroli
- **Mix & Match** - dowolne kombinacje funkcjonalnoúci
- **Future-proof** - ≥atwe dodawanie nowych typÛw danych

### 3. Maintenance Excellence
- **Single Source of Truth** - wszystkie listy w jednym komponencie
- **Consistent Behavior** - identyczna logika wszÍdzie
- **Easy Updates** - zmiana w jednym miejscu = update wszystkich list

### 4. Performance & Scalability
- **Reused Components** - mniej instancji objektÛw
- **Consistent Styling** - jednolite style performance
- **Standardized Interactions** - przewidywalne zachowanie

## Wykorzystanie w Przysz≥oúci

UniversalListItemComponent moøe obs≥uøyÊ przysz≥e typy danych przez:

### Nowe Typy Danych
- **PlannedMeal** - zaplanowane posi≥ki
- **ShoppingItem** - elementy listy zakupÛw  
- **User** - uøytkownicy (jeúli multi-user)
- **Category** - kategorie przepisÛw/sk≥adnikÛw

### Nowe Parametry Konfiguracjipublic bool ShowImageThumbnail { get; set; } = false;
public bool ShowStatusBadge { get; set; } = false;  
public bool ShowQuantityPicker { get; set; } = false;
public string LayoutMode { get; set; } = "Standard"; // Standard, Compact, Expanded
### Nowe Layout Modes
- **Compact Mode** - minimalistyczny widok
- **Expanded Mode** - szczegÛ≥owy widok z dodatkowymi informacjami
- **Card Mode** - layout w stylu kart
- **Table Mode** - tabelaryczny layout

## Wzorce Zastosowane

### 1. Strategy Pattern + Configuration
- RÛøne strategie wyúwietlania przez parametry boolean
- Runtime configuration zamiast compile-time inheritance

### 2. Template Method Pattern Extended
- WspÛlny szablon z wieloma punktami konfiguracji
- Conditional rendering przez IsVisible bindings

### 3. Open/Closed Principle Maximum
- ZamkniÍty na modyfikacje struktury
- Maksymalnie otwarty na nowe konfiguracje

### 4. Single Responsibility with Multi-Context
- Jedna odpowiedzialnoúÊ: wyúwietlanie list items
- Obs≥uga wielu kontekstÛw przez parametryzacjÍ

## Kolejne Kroki i Rozszerzenia

### 1. Advanced Configurationpublic enum ComponentMode { Recipe, Ingredient, Plan, Custom }
public ComponentMode Mode { get; set; } = ComponentMode.Custom;
// Auto-configure wszystkie w≥aúciwoúci based on Mode
### 2. Custom Content Templates<components:UniversalListItemComponent>
    <components:UniversalListItemComponent.CustomContent>
        <DataTemplate>
            <!-- Custom layout for specific cases -->
        </DataTemplate>
    </components:UniversalListItemComponent.CustomContent>
</components:UniversalListItemComponent>
### 3. Dynamic Action Configurationpublic ObservableCollection<ActionButtonConfig> ActionButtons { get; set; }
// Dynamically add/remove action buttons
## Podsumowanie Refaktoryzacji

### Faza 1: WyodrÍbnienie KomponentÛw (5 komponentÛw)
- **Redukcja duplikacji o ~50%** w Recipe/Ingredients pages
- **Podstawowa modularnoúÊ** osiπgniÍta

### Faza 2: Unifikacja Recipe/Ingredient (4 komponenty) 
- **Po≥πczenie podobnych komponentÛw** w jeden uniwersalny
- **Flexible configuration** przez `ShowSubtitle`

### Faza 3: Multi-Mode Extension (4 komponenty) ? **AKTUALNA**
- **Dodanie obs≥ugi Plan** jako trzeciego typu danych
- **ShoppingListPage refaktoryzacja** z dedykowanej struktury na uniwersalny komponent
- **5 nowych parametrÛw** konfiguracji dla maksymalnej elastycznoúci
- **3 rÛøne tryby** pracy: Recipe, Ingredient, Plan

### Wyniki KoÒcowe
? **DRY Principle** - Zero duplikacji miÍdzy 3 stronami list  
? **SOLID Principles** - Single responsibility, Open/Closed maximum compliance  
? **Maintenance** - Jeden komponent dla wszystkich typÛw list  
? **Flexibility** - 8 parametrÛw konfiguracji dla dowolnych kombinacji  
? **Performance** - Minimum komponentÛw, maksimum reuøywalnoúci  
? **Scalability** - £atwe dodawanie nowych typÛw danych  
? **Consistency** - Identyczne zachowanie we wszystkich listach  

### Struktura Finalna KomponentÛwViews/Components/
??? ModernSearchBarComponent.xaml (.cs)
??? GenericListComponent.xaml (.cs)  
??? FloatingActionButtonComponent.xaml (.cs)
??? UniversalListItemComponent.xaml (.cs) ? **MULTI-MODE**
??? COMPONENTS-DOCUMENTATION.md
### Strony Zrefaktoryzowane? RecipesPage.xaml - Recipe Mode
? IngredientsPage.xaml - Ingredient Mode  
? ShoppingListPage.xaml - Plan Mode ? **NOWA**
**Kompilacja**: ? **Build Successful**  
**FunkcjonalnoúÊ**: ? **Pe≥na kompatybilnoúÊ wszystkich stron**  
**Kod**: ? **Zero duplikacji w 3 stronach**  
**Komponenty**: ? **Ultra-reuøywalne dla kaødego typu danych**
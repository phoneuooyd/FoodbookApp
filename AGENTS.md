# AGENTS.md

## 🤖 Przewodnik dla Agentów AI - FoodBook App

**Aplikacja mobilna do zarządzania przepisami, planowania posiłków i tworzenia list zakupów**

---

## 🛠️ Konfiguracja Techniczna

### Framework i Technologie
- **Framework**: .NET MAUI (Multi-platform App UI)
- **Wersja .NET**: 9.0
- **Język**: C# 13.0
- **Platformy docelowe**: Android, iOS, Windows, macOS
- **Baza danych**: SQLite z Entity Framework Core 9.0.6
- **UI**: XAML z Material Design
- **Wzorce architektoniczne**: MVVM, Dependency Injection, Clean Architecture

### Główne Zależności
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.6" />
<PackageReference Include="HtmlAgilityPack" Version="1.12.1" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
<PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="9.0.0" />
### Cel Projektu
Kompleksowa aplikacja mobilna do:
- Zarządzania bazą przepisów kulinarnych z importem z internetu
- Organizowania składników z informacjami odżywczymi
- Planowania posiłków na wybrane dni z konfiguracją liczby posiłków
- Automatycznego generowania list zakupów z funkcją zaznaczania
- Obliczania kalorii i makroskładników (białko, tłuszcze, węglowodany)
- Archiwizowania i przywracania planów żywieniowych

---

## 🏗️ Preferencje Technologiczne

### Wzorce Architektoniczne
- **MVVM Pattern**: Separacja logiki biznesowej od UI
- **Dependency Injection**: Constructor injection dla wszystkich zależności
- **Clean Architecture**: Jasne warstwy (Models, Services, ViewModels, Views)
- **Repository Pattern**: Abstrakcja dostępu do danych
- **Observer Pattern**: INotifyPropertyChanged dla binding

### Styl Kodu
- **Microsoft C# Conventions**: PascalCase dla publicznych członków
- **Async/Await**: Dla wszystkich operacji I/O
- **Nullable Reference Types**: Włączone globalnie
- **XML Documentation**: Dla wszystkich publicznych API
- **Resource Files**: Dla lokalizacji (`.resx`)

### Testowanie
- **Framework**: xUnit
- **Mocking**: Moq
- **Assertions**: FluentAssertions
- **Coverage**: Minimum 80% dla business logic

---

## 🎨 Standardy UI/UX dla Aplikacji Mobilnych

### Design System
- **Material Design**: Zgodność z wytycznymi Google/Microsoft
- **Light/Dark Mode**: Automatyczne przełączanie na podstawie ustawień systemu
- **Responsive Layout**: Adaptacja do różnych rozmiarów ekranów
- **Touch-First**: Minimalna wielkość przycisków 44x44 px
- **Accessibility**: Support dla Screen Readers i High Contrast

### Nawigacja
- **Shell Navigation**: Hierarchiczna struktura z Tab Bar
- **Flyout Menu**: Dla głównych sekcji aplikacji
- **Modal Pages**: Dla formularzy i szczegółów
- **Back Button**: Konsystentne zachowanie na wszystkich platformach

### Loading States
- **ProgressBar**: Dla długotrwałych operacji
- **ActivityIndicator**: Dla krótkich operacji
- **Skeleton Screens**: Dla ładowania list
- **Pull-to-Refresh**: Dla aktualizacji danych

---

## ⚙️ Konfiguracja Środowiska

### Visual Studio 2022 Requirements
- **Minimalna wersja**: 17.8+
- **Workloads**: .NET MAUI development
- **SDK**: .NET 9.0
- **Emulatory**: Android API 21+, iOS 15.0+

### Struktura ProjektuFoodbookApp/
├── Data/           # Entity Framework DbContext i modele
├── Models/         # Modele domenowe
├── Services/       # Serwisy biznesowe i interfejsy
├── ViewModels/     # ViewModels dla MVVM
├── Views/          # Strony XAML
├── Localization/   # Pliki zasobów .resx
├── Resources/      # Obrazy, fonty, style
└── Platforms/      # Platform-specific code
---

## 🤖 Prompty dla Agentów AI

### Code Generation Guidelines

#### 1. MVVM Pattern z Base Classes// Preferowany wzorzec dla ViewModels
public class ExampleViewModel : INotifyPropertyChanged
{
    private readonly IExampleService _service;
    
    public ExampleViewModel(IExampleService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        LoadCommand = new Command(async () => await LoadDataAsync());
    }
    
    public ICommand LoadCommand { get; }
    
    private async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;
            var data = await _service.GetDataAsync();
            // Process data...
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Błąd", ex.Message, "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
#### 2. Async/Await dla I/O Operations// Zawsze używaj async/await dla database i HTTP calls
public async Task<List<Recipe>> GetRecipesAsync()
{
    using var scope = _serviceProvider.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    
    return await context.Recipes
        .Include(r => r.Ingredients)
        .ToListAsync();
}
#### 3. Proper Disposal Patternpublic class DatabaseService : IDisposable
{
    private readonly AppDbContext _context;
    private bool _disposed = false;
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _context?.Dispose();
        }
        _disposed = true;
    }
}
#### 4. Dependency Injection Registration// W MauiProgram.cs
builder.Services.AddScoped<IRecipeService, RecipeService>();
builder.Services.AddTransient<AddRecipeViewModel>();
builder.Services.AddSingleton<ILocalizationService, LocalizationService>();

#### 5. Localization Support// Używaj Resource files
public string Title => AddRecipePageResources.Title;
public string SaveButton => ButtonResources.Save;

// W XAML
<Label Text="{x:Static resources:AddRecipePageResources.Title}" />
---

## 🧪 Testing Guidelines

### Unit Test Structure
[Fact]
public async Task LoadRecipes_WhenServiceSucceeds_ShouldPopulateRecipes()
{
    // Arrange
    var mockService = new Mock<IRecipeService>();
    var expectedRecipes = new List<Recipe> { new Recipe { Name = "Test" } };
    mockService.Setup(s => s.GetRecipesAsync()).ReturnsAsync(expectedRecipes);
    
    var viewModel = new RecipeViewModel(mockService.Object);
    
    // Act
    await viewModel.LoadCommand.ExecuteAsync(null);
    
    // Assert
    viewModel.Recipes.Should().NotBeEmpty();
    viewModel.Recipes.Should().HaveCount(1);
    viewModel.Recipes.First().Name.Should().Be("Test");
}
### Integration Tests
[Fact]
public async Task AddRecipe_ShouldSaveToDatabase()
{
    // Arrange
    using var context = CreateInMemoryContext();
    var service = new RecipeService(context);
    var recipe = new Recipe { Name = "New Recipe" };
    
    // Act
    await service.AddRecipeAsync(recipe);
    
    // Assert
    var savedRecipe = await context.Recipes.FirstOrDefaultAsync();
    savedRecipe.Should().NotBeNull();
    savedRecipe.Name.Should().Be("New Recipe");
}
---

## 🔍 Code Review Focus Areas

### Performance
- **Memory Leaks**: Sprawdzaj event unsubscription w ViewModels
- **Database Queries**: Używaj Include() tylko gdy potrzebne
- **Collections**: ObservableCollection dla UI binding
- **Caching**: Implementuj dla często używanych danych

// ✅ Dobra praktyka - unsubscribe events
protected override void OnDisappearing()
{
    viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    base.OnDisappearing();
}

// ✅ Efficient database query
var recipes = await context.Recipes
    .Where(r => r.Name.Contains(searchTerm))
    .Take(20)
    .ToListAsync();

### Platform Compatibility
- **iOS/Android Differences**: Sprawdzaj navigation behavior
- **File System**: Używaj FileSystem.AppDataDirectory
- **Permissions**: Sprawdzaj runtime permissions
- **UI Rendering**: Testuj na różnych rozdzielczościach

---

## 🔒 Security Guidelines

### Data Protection// Sensitive data w Secure Storage
await SecureStorage.SetAsync("api_key", apiKey);
var storedKey = await SecureStorage.GetAsync("api_key");
### Privacy & GDPR
- **User Consent**: Jawne zgody na przetwarzanie danych
- **Data Anonymization**: Usuwanie danych osobowych
- **Local Storage**: Tylko niezbędne dane
- **Export/Delete**: Funkcje eksportu i usuwania danych użytkownika

### Database Security// Encryption dla SQLite (jeśli potrzebna)
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var dbPath = Path.Combine(FileSystem.AppDataDirectory, "foodbook.db");
    options.UseSqlite($"Data Source={dbPath};Password=your_password");
});
---

## 📚 Przydatne Linki i Zasoby

### Dokumentacja
- [.NET MAUI Docs](https://docs.microsoft.com/en-us/dotnet/maui/)
- [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/)
- [MVVM Pattern](https://docs.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
- [Material Design](https://material.io/design)

### Best Practices
- [MAUI Performance](https://docs.microsoft.com/en-us/dotnet/maui/performance)
- [Cross-Platform Development](https://docs.microsoft.com/en-us/dotnet/maui/platform-integration/)
- [Accessibility Guidelines](https://docs.microsoft.com/en-us/dotnet/maui/fundamentals/accessibility)

---

## 🏷️ AI Labels i Tagi
#dotnet-maui #blazor-hybrid #dotnet9 #mvvm #mobile-development 
#cross-platform #xaml #csharp #entity-framework #sqlite
#food-recipes #meal-planning #shopping-lists #nutrition-tracking
#material-design #localization #dependency-injection
---

## 🤝 Współpraca z AI

### Oczekiwania od AI
1. **MAUI Best Practices**: Zawsze sprawdzaj zgodność z najnowszymi wytycznymi
2. **Platform Differences**: Pamiętaj o różnicach iOS vs Android
3. **.NET 9 Features**: Używaj najnowszych funkcji językowych
4. **Performance First**: Optymalizuj dla urządzeń mobilnych
5. **Accessibility**: Zawsze dodawaj AutomationProperties
6. **Localization**: Używaj Resource files zamiast hardcoded strings

### Feedback Loop
Jeśli AI-generated code ma problemy:
1. ✅ Sprawdź zgodność z tym dokumentem
2. ✅ Zweryfikuj platform compatibility  
3. ✅ Uruchom testy jednostkowe
4. ✅ Przetestuj na emulatorach/urządzeniach
5. ✅ Sprawdź memory usage w profilerze

### Code Quality Checklist
- [ ] Async/await dla wszystkich I/O operations
- [ ] Proper error handling z try/catch
- [ ] IDisposable pattern gdzie potrzebny
- [ ] XML documentation dla publicznych API
- [ ] Resource files dla UI strings
- [ ] Unit tests coverage > 80%
- [ ] No memory leaks w ViewModels
- [ ] Platform-specific code w Platforms/

---

**Ostatnia aktualizacja**: 19.08.2025  
**Wersja**: 1.0  
**Autor**: phoneuyood

---

> 💡 **Tip dla AI**: Zawsze sprawdzaj ten dokument przed generowaniem kodu dla FoodBook App. Priorytetyzuj performance, accessibility i cross-platform compatibility.
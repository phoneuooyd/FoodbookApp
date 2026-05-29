Twój przyk³ad œwietnie pokazuje ró¿nicê miêdzy "zielonymi" testami jednostkowymi, które daj¹ fa³szywe poczucie bezpieczeñstwa, a prawdziw¹ regresj¹. Rzuæmy okiem na kod, o którym wspomnia³eœ:

```csharp
// ? Test, który nie testuje ¿adnej logiki biznesowej ani integracji
[Theory]
[InlineData(0.0)]
[InlineData(150.5)]
[InlineData(500.0)]
public void Recipe_NutritionalValues_WhenSetToValidValues_ShouldUpdateCorrectly(double value)
{
    // Arrange
    var recipe = new Recipe();
    // Act
    recipe.Calories = value;
    // ...
    // Assert
    Assert.Equal(value, recipe.Calories);
    // ...
}
```

Ten test weryfikuje jedynie, ¿e mechanizm `get` i `set` w C# dzia³a (i to tylko dla prostych typów). Zupe³nie pomija, czy dane te s¹ poprawnie mapowane do bazy danych, czy ViewModel w³aœciwie je formatuje, a co najwa¿niejsze – czy w ogóle trafiaj¹ do UI.

Dobra wiadomoœæ jest taka, ¿e mo¿na to zrobiæ znacznie lepiej. Oto praktyczny zestaw narzêdzi i wzorców, które pomog¹ Ci tworzyæ niezawodne testy regresyjne w aplikacjach mobilnych .NET (MAUI/Xamarin).

### ?? Strategia Testowania: Piramida Testów Regresyjnych

Kluczem jest podejœcie warstwowe, gdzie najwiêcej testów to szybkie testy jednostkowe logiki, a na szczycie jest mniejsza liczba wolniejszych, ale kompleksowych testów UI.

*   **Warstwa 1: Testy Jednostkowe ViewModeli (Podstawa)** – Szybkie, liczne i odizolowane od UI oraz bazy danych. Testuj¹ logikê biznesow¹.
*   **Warstwa 2: Testy Integracyjne z Baz¹ Danych** – Wolniejsze, ale weryfikuj¹ kluczowy punkt awarii, jakim jest warstwa danych (zapis/odczyt).
*   **Warstwa 3: Automatyczne Testy UI (Szczyt)** – Najwolniejsze i bardziej "kruche". Skupiaj¹ siê na najwa¿niejszych œcie¿kach biznesowych (np. logowanie, zakup).

### ?? Testowanie ViewModeli: Logika to nie wszystko

ViewModel to centrum dowodzenia. Testy musz¹ sprawdzaæ, co robi, a nie jakie ma w³aœciwoœci. Kluczowe jest uniezale¿nienie go od platformy i frameworka UI poprzez wstrzykiwanie zale¿noœci (Dependency Injection).

#### Dobra Praktyka: Testowanie Zachowania (Behaviour Testing)

Zamiast sprawdzaæ ustawienie propercji, przetestujmy efekt wywo³ania komendy:

```csharp
// ? Dobrze: Testujemy scenariusz zapisu przepisu
[Fact]
public async Task SaveRecipeCommand_WhenExecuted_ShouldCallRepositoryAndNavigate()
{
    // Arrange
    var mockRecipeRepo = new Mock<IRecipeRepository>();
    var mockNavService = new Mock<INavigationService>();
    var viewModel = new AddRecipeViewModel(mockRecipeRepo.Object, mockNavService.Object);
    viewModel.RecipeName = "Carbonara";

    // Act
    await viewModel.SaveRecipeCommand.ExecuteAsync(null);

    // Assert
    // 1. Sprawdzamy, czy metoda repozytorium zosta³a wywo³ana z konkretnym obiektem
    mockRecipeRepo.Verify(repo => repo.SaveAsync(It.Is<Recipe>(r => r.Name == "Carbonara")), Times.Once);
    // 2. Sprawdzamy, czy aplikacja nawigowa³a do innego widoku
    mockNavService.Verify(nav => nav.GoBackAsync(), Times.Once);
}
```

#### Dobra Praktyka: Testowanie Powiadomieñ o Zmianie W³aœciwoœci (INotifyPropertyChanged)

To kluczowe dla poprawnego dzia³ania UI. Zamiast rêcznie podpinaæ siê pod eventy, u¿yj dedykowanych bibliotek, aby test by³ czytelny i niezawodny.

```csharp
// ? Dobrze: U¿ycie NotifyPropertyChanged.Verifier
[Fact]
public void RecipeName_WhenSet_ShouldRaisePropertyChangedEvent()
{
    var vm = new RecipeViewModel();
    vm.ShouldNotifyOn(vm => vm.RecipeName)
      .When(vm => vm.RecipeName = "Nowa nazwa");
}
```

### ??? Testowanie Integracji z Baz¹ Danych: Prawdziwa Weryfikacja Zapisów

Tu w³aœnie testy jednostkowe zawodz¹. Musisz przetestowaæ rzeczywist¹ interakcjê z silnikiem bazy danych. Najlepszym rozwi¹zaniem jest u¿ycie bazy danych SQLite w pamiêci (in-memory), która jest szybka i nie wymaga zarz¹dzania plikami, ale wiernie odwzorowuje dzia³anie relacyjnej bazy danych.

```csharp
// ? Dobrze: Test integracyjny z baz¹ danych w pamiêci
[Fact]
public async Task SaveRecipeAsync_ShouldPersistRecipeToDatabase()
{
    // Arrange: Tworzymy unikalne po³¹czenie do bazy w pamiêci dla ka¿dego testu
    var connection = new SqliteConnection("Filename=:memory:");
    connection.Open();
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;
        
    using (var context = new AppDbContext(options))
    {
        await context.Database.EnsureCreatedAsync(); // Tworzymy schemat
        var repository = new RecipeRepository(context);
        var newRecipe = new Recipe { Name = "Testowy Przepis", Calories = 100 };

        // Act
        await repository.SaveAsync(newRecipe);
    }

    // Assert: Sprawdzamy w nowym kontekœcie, czy dane zosta³y faktycznie zapisane
    using (var context = new AppDbContext(options))
    {
        var savedRecipe = await context.Recipes.FirstOrDefaultAsync(r => r.Name == "Testowy Przepis");
        Assert.NotNull(savedRecipe);
        Assert.Equal(100, savedRecipe.Calories);
    }
}
```

### ?? Testowanie UI: Automatyzacja Tego, Co Widzi U¿ytkownik

Testy UI s¹ niezast¹pione do wykrywania problemów z wyœwietlaniem i nawigacj¹. W ekosystemie .NET MAUI/Xamarin u¿ywa siê do tego **Xamarin.UITest** lub **Appium**.

#### Dobra Praktyka: Wzorzec Page Object Model (POM)

Nigdy nie pisz testów UI, które bezpoœrednio odwo³uj¹ siê do selektorów w ka¿dym teœcie. U¿yj wzorca POM, aby oddzieliæ logikê testu od szczegó³ów implementacji UI. Dziêki temu testy s¹ odporne na zmiany w interfejsie.

```csharp
// ? Dobrze: Page Object dla widoku przepisu
public class RecipeDetailPage
{
    private readonly IApp _app;
    public RecipeDetailPage(IApp app) => _app = app;
    
    // Lokalizacja elementów UI jest w jednym miejscu
    public void WaitForPageToLoad() => _app.WaitForElement("recipeDetailPage");
    public string GetRecipeName() => _app.Query("recipeNameLabel").First().Text;
    public string GetCalories() => _app.Query("caloriesLabel").First().Text;
    public void TapEditButton() => _app.Tap("editRecipeButton");
}
```

#### Dobra Praktyka: Testowanie Kolorów w UI

Weryfikacja kolorów w testach UI jest mo¿liwa, choæ wymaga znajomoœci natywnych mechanizmów platformy. Dla Androida mo¿na to zrobiæ, wywo³uj¹c metody platformy za pomoc¹ `Invoke`.

```csharp
// ? Dobrze: Sprawdzanie koloru przycisku na Androidzie za pomoc¹ Xamarin.UITest
[Test]
public void SaveButton_WhenRecipeIsValid_ShouldBeGreen()
{
    // ... logika testu ...
    
    // Poczekaj, a¿ element bêdzie dostêpny
    app.WaitForElement(c => c.Marked("saveRecipeButton"));
    
    // Wywo³aj natywne metody Androida, aby pobraæ kolor t³a
    var colorId = app.Query(c => c.Marked("saveRecipeButton")
        .Invoke("getBackground")
        .Invoke("getColor"))
        .FirstOrDefault();
    
    // Pobierz kontekst aplikacji, aby zmapowaæ ID koloru na rzeczywist¹ wartoœæ
    var context = app.Query(c => c.Marked("saveRecipeButton").Invoke("getContext")).First();
    var actualColor = context.Invoke("getColor", colorId);
    
    // Porównaj z oczekiwan¹ wartoœci¹ (np. Color.Green)
    Assert.AreEqual(expectedColor, actualColor);
}
```

### ?? Antywzorców, Których Nale¿y Unikaæ

W testowaniu równie wa¿ne jest wiedzieæ, czego **nie robiæ**.

*   **Testowanie implementacji, a nie zachowania (Testing Implementation Details)**: Unikaj testowania prywatnych metod za pomoc¹ refleksji. Jeœli prywatna metoda jest tak z³o¿ona, ¿e wymaga testów, to znak, ¿e powinna zostaæ wydzielona do osobnej, publicznej klasy.
*   **Nadmierne mockowanie (Over-Mocking)**: Nie mockuj wszystkiego. Mockuj tylko "bolesne" zale¿noœci, takie jak baza danych, serwisy HTTP czy system plików. Dla prostych obiektów pomocniczych (np. walidatorów, mapperów) u¿ywaj prawdziwych implementacji. Testy stan¹ siê czytelniejsze i bardziej wartoœciowe.
*   **U¿ywanie tych samych danych testowych dla wszystkich testów (Shared State Corruption)**: Ka¿dy test powinien byæ odizolowany. Jeœli jeden test zmodyfikuje statyczne dane lub dane w bazie, mo¿e to wp³yn¹æ na wyniki innych testów. Zawsze sprz¹taj po sobie lub u¿ywaj transakcji/baz w pamiêci, aby zapewniæ czysty stan na start.
*   **Pisanie testów dla kodu, który nie jest Twój (Testing Framework Code)**: Twój przyk³ad z testowaniem `get`/`set` idealnie to ilustruje. Nie testujesz tam swojego kodu – testujesz, czy kompilator C# i œrodowisko uruchomieniowe dzia³aj¹ poprawnie. To strata czasu. Skup siê na swojej logice.
*   **U¿ywanie "magicznych stringów" w testach UI**: Unikaj wyszukiwania elementów po tekœcie, który mo¿e siê zmieniæ przy lokalizacji. Zamiast tego u¿ywaj `AutomationId`, który jest stabilny i przeznaczony w³aœnie do celów testowych.

Mam nadziejê, ¿e te wytyczne pomog¹ Ci zbudowaæ solidn¹ strategiê testów regresyjnych. Powodzenia!
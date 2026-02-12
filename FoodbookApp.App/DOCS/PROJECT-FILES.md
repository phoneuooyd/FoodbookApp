# PROJECT-FILES.md

<!-- This file will contain information about the project structure and file organization of the FoodBook App -->

Kompletny spis plików projektu FoodbookApp (bez plików tymczasowych bin/obj poza artefaktami platform – mo¿na oczyœciæ przy potrzebie). Stan na moment generacji.

## Struktura katalogów (high-level)FoodbookApp/
??? App.xaml.cs
??? AppShell.xaml / AppShell.xaml.cs
??? FoodbookApp.csproj
??? MauiProgram.cs
??? Data/
??? Models/
??? Services/
??? ViewModels/
??? Views/
??? Converters/
??? Localization/
??? Resources/
?   ??? AppIcon/
?   ??? Fonts/
?   ??? Images/
?   ??? Raw/
?   ??? Splash/
?   ??? Styles/
??? Platforms/
?   ??? Android/
?   ??? iOS/
?   ??? MacCatalyst/
?   ??? Tizen/
?   ??? Windows/
??? Properties/
??? AGENTS.md
??? README.md (jeœli istnieje w katalogu g³ównym)
??? DOCUMENTATION-GUIDE.md
??? PROJECT-FILES.md
## Pliki Ÿród³owe C# (code)
### Root
- App.xaml.cs  
- AppShell.xaml  
- AppShell.xaml.cs  
- MauiProgram.cs  
- FoodbookApp.csproj

### Data
- Data/AppDbContext.cs  
- Data/SeedData.cs

### Models
- Models/Ingredient.cs  
- Models/PlannedMeal.cs  
- Models/PlannerDay.cs  
- Models/Plan.cs  
- Models/Recipe.cs

### Services (interfejsy + implementacje + lokalizacja + importer)
- Services/IIngredientService.cs  
- Services/ILocalizationService.cs  
- Services/IPlannerService.cs  
- Services/IPlanService.cs  
- Services/IRecipeService.cs  
- Services/IShoppingListService.cs  
- Services/IngredientService.cs  
- Services/LocalizationResourceManager.cs  
- Services/LocalizationService.cs  
- Services/PlannerService.cs  
- Services/PlanService.cs  
- Services/RecipeImporter.cs  
- Services/RecipeService.cs  
- Services/ShoppingListService.cs  
- Services/TranslateExtension.cs

### ViewModels
- ViewModels/AddRecipeViewModel.cs  
- ViewModels/ArchiveViewModel.cs  
- ViewModels/HomeViewModel.cs  
- ViewModels/IngredientFormViewModel.cs  
- ViewModels/IngredientsViewModel.cs  
- ViewModels/PlannedMealFormViewModel.cs  
- ViewModels/PlannerViewModel.cs  
- ViewModels/RecipeViewModel.cs  
- ViewModels/SettingsViewModel.cs  
- ViewModels/ShoppingListDetailViewModel.cs  
- ViewModels/ShoppingListViewModel.cs

### Converters
- Converters/BoolToColorConverter.cs  
- Converters/InvertedBoolConverter.cs

### Views (XAML + code-behind)
- Views/AddRecipePage.xaml  
- Views/AddRecipePage.xaml.cs  
- Views/ArchivePage.xaml  
- Views/ArchivePage.xaml.cs  
- Views/HomePage.xaml  
- Views/HomePage.xaml.cs  
- Views/IngredientFormPage.xaml  
- Views/IngredientFormPage.xaml.cs  
- Views/IngredientsPage.xaml  
- Views/IngredientsPage.xaml.cs  
- Views/MealFormPage.xaml  
- Views/MealFormPage.xaml.cs  
- Views/PlannerPage.xaml  
- Views/PlannerPage.xaml.cs  
- Views/RecipesPage.xaml  
- Views/RecipesPage.xaml.cs  
- Views/SettingsPage.xaml  
- Views/SettingsPage.xaml.cs  
- Views/ShoppingListDetailPage.xaml  
- Views/ShoppingListDetailPage.xaml.cs  
- Views/ShoppingListPage.xaml  
- Views/ShoppingListPage.xaml.cs

## Lokalizacja (Resources .resx + generowane Designery)
- Localization/AddRecipePageResources.resx  
- Localization/AddRecipePageResources.pl-PL.resx  
- Localization/AddRecipePageResources.Designer.cs  
- Localization/ArchivePageResources.resx  
- Localization/ArchivePageResources.pl-PL.resx  
- Localization/ArchivePageResources.Designer.cs  
- Localization/ButtonResources.resx  
- Localization/ButtonResources.pl-PL.resx  
- Localization/ButtonResources.Designer.cs  
- Localization/HomePageResources.resx  
- Localization/HomePageResources.pl-PL.resx  
- Localization/HomePageResources.Designer.cs  
- Localization/IngredientFormPageResources.resx  
- Localization/IngredientFormPageResources.pl-PL.resx  
- Localization/IngredientFormPageResources.Designer.cs  
- Localization/IngredientsPageResources.resx  
- Localization/IngredientsPageResources.pl-PL.resx  
- Localization/IngredientsPageResources.Designer.cs  
- Localization/MealFormPageResources.resx  
- Localization/MealFormPageResources.pl-PL.resx  
- Localization/MealFormPageResources.Designer.cs  
- Localization/PlannerPageResources.resx  
- Localization/PlannerPageResources.pl-PL.resx  
- Localization/PlannerPageResources.Designer.cs  
- Localization/RecipesPageResources.resx  
- Localization/RecipesPageResources.pl-PL.resx  
- Localization/RecipesPageResources.Designer.cs  
- Localization/SettingsPageResources.resx  
- Localization/SettingsPageResources.pl-PL.resx  
- Localization/SettingsPageResources.Designer.cs  
- Localization/ShoppingListDetailPageResources.resx  
- Localization/ShoppingListDetailPageResources.pl-PL.resx  
- Localization/ShoppingListDetailPageResources.Designer.cs  
- Localization/ShoppingListPageResources.resx  
- Localization/ShoppingListPageResources.pl-PL.resx  
- Localization/ShoppingListPageResources.Designer.cs  
- Localization/TabBarResources.resx  
- Localization/TabBarResources.pl-PL.resx  
- Localization/TabBarResources.Designer.cs

## Zasoby (Resources)
### AppIcon
- Resources/AppIcon/appicon.png  
- Resources/AppIcon/appicon.svg  
- Resources/AppIcon/appiconfg.svg

### Fonts
- Resources/Fonts/OpenSans-Regular.ttf  
- Resources/Fonts/OpenSans-Semibold.ttf

### Images
- Resources/Images/chef_hat.png  
- Resources/Images/event_list.png  
- Resources/Images/event_upcoming.png  
- Resources/Images/grocery.png  
- Resources/Images/home.png

### Raw
- Resources/Raw/AboutAssets.txt  
- Resources/Raw/ingredients.json

### Splash
- Resources/Splash/appsplash.png  
- Resources/Splash/splash.svg

### Styles
- Resources/Styles/Colors.xaml  
- Resources/Styles/Styles.xaml

## Platforms
### Android
- Platforms/Android/AndroidManifest.xml  
- Platforms/Android/MainActivity.cs  
- Platforms/Android/MainApplication.cs  
- Platforms/Android/Resources/values/colors.xml

### iOS
- Platforms/iOS/AppDelegate.cs  
- Platforms/iOS/Info.plist  
- Platforms/iOS/Program.cs  
- Platforms/iOS/Resources/PrivacyInfo.xcprivacy

### MacCatalyst
- Platforms/MacCatalyst/AppDelegate.cs  
- Platforms/MacCatalyst/Entitlements.plist  
- Platforms/MacCatalyst/Info.plist  
- Platforms/MacCatalyst/Program.cs

### Tizen
- Platforms/Tizen/Main.cs  
- Platforms/Tizen/tizen-manifest.xml

### Windows
- Platforms/Windows/App.xaml  
- Platforms/Windows/App.xaml.cs  
- Platforms/Windows/app.manifest  
- Platforms/Windows/Package.appxmanifest

## Properties
- Properties/launchSettings.json

## Dokumentacja / Meta
- AGENTS.md  
- DOCUMENTATION-GUIDE.md  
- PROJECT-FILES.md  
- README.md (je¿eli w repo; nie zawsze widoczny w tej liœcie)  

## Wygenerowane / Tymczasowe (przyk³ad – mo¿na pomin¹æ przy przegl¹dzie kodu)
Pliki w katalogach obj/ i bin/ zawieraj¹ artefakty kompilacji (AssemblyInfo, XamlTypeInfo, zasoby wygenerowane przez Resizetizer itd.) i nie s¹ rêcznie edytowane. Przy potrzebie pe³nego audytu mo¿na je dopisaæ.

---
**Uwaga**: Jeœli dodasz nowe modu³y (np. Tests/, Scripts/, Tools/), zaktualizuj ten dokument.
# Migration Guide: Plan Type Separation

## Problem
- Plannery i listy zakupów u¿ywa³y tego samego modelu `Plan`
- Wszystkie plany mia³y `Label = "Lista zakupów"`
- Archiwizacja plannera archiwizowa³a równie¿ listê zakupów
- Brak mo¿liwoœci rozró¿nienia w widoku archiwum

## Rozwi¹zanie

### 1. Dodano enum `PlanType`
```csharp
public enum PlanType
{
    Planner,        // Plany posi³ków
    ShoppingList    // Listy zakupów
}
```

### 2. Zaktualizowano model `Plan`
- Dodano w³aœciwoœæ `Type` (typ: `PlanType`)
- Dodano opcjonaln¹ w³aœciwoœæ `PlannerName` (do przysz³ych nazw u¿ytkownika)
- `Name` - pokazuje nazwê niestandardow¹ lub domyœln¹ w zale¿noœci od typu
- `Label` - zwraca "Planner" lub "Lista zakupów" w zale¿noœci od typu

### 3. Zaktualizowano ViewModels

#### PlannerViewModel
- Ustawia `Type = PlanType.Planner` przy tworzeniu nowego planu
- Plany posi³ków s¹ teraz oddzielone od list zakupów

#### PlannerListsViewModel  
- Filtruje tylko plany typu `Planner`
- Dodano `ArchivePlanCommand`
- Powiadamia `AppEvents` o zmianach

#### ShoppingListViewModel
- Filtruje tylko plany typu `ShoppingList`
- Powiadamia `AppEvents` o zmianach archiwizacji

#### ArchiveViewModel
- Dwie osobne kolekcje: `ArchivedPlanners` i `ArchivedShoppingLists`
- Rozdziela zarchiwizowane elementy wed³ug `Type`
- Nas³uchuje `AppEvents.PlanChangedAsync` dla automatycznego odœwie¿ania
- Komunikaty w dialogach dostosowane do typu (planner/lista zakupów)

### 4. Zaktualizowano widok ArchivePage
- Dwie sekcje z nag³ówkami:
  - "Zarchiwizowane plannery"
  - "Zarchiwizowane listy zakupów"
- Ka¿da sekcja u¿ywa `GenericListComponent`
- Osobne komunikaty pustych list

### 5. Konfiguracja bazy danych
```csharp
modelBuilder.Entity<Plan>()
    .Property(p => p.Type)
    .HasConversion<int>()
    .HasDefaultValue(PlanType.Planner);
```

## Migracja istniej¹cych danych

### Automatyczna migracja
Entity Framework automatycznie doda kolumnê `Type` do tabeli `Plans` z wartoœci¹ domyœln¹ `0` (Planner).

### Rêczna migracja (jeœli potrzebna)
Jeœli masz istniej¹ce listy zakupów, które powinny mieæ `Type = ShoppingList`, wykonaj:

```sql
-- Opcjonalnie: ustaw Type na ShoppingList dla starszych planów
-- (dostosuj logikê wed³ug potrzeb)
UPDATE Plans SET Type = 1 WHERE /* warunki identyfikuj¹ce listy zakupów */
```

### Sprawdzenie migracji
Po pierwszym uruchomieniu aplikacji:
1. SprawdŸ, czy istniej¹ce plany wyœwietlaj¹ siê poprawnie
2. Utwórz nowy planner - sprawdŸ czy ma `Type = Planner`
3. Zarchiwizuj planner - sprawdŸ czy pojawia siê w sekcji "Zarchiwizowane plannery"

## Testowanie

### Przypadki testowe
1. ? Tworzenie nowego plannera ? Type = Planner
2. ? Archiwizacja plannera ? pojawia siê w sekcji plannerów
3. ? Lista zakupów nie archiwizuje siê razem z plannerem
4. ? Przywracanie plannera z archiwum
5. ? Usuwanie plannera z archiwum
6. ? Oddzielne liczniki dla plannerów i list zakupów

## Notatki
- Wszystkie nowe plany tworzone przez `PlannerViewModel` maj¹ `Type = Planner`
- Wszystkie nowe plany tworzone przez widok list zakupów maj¹ `Type = ShoppingList`
- `AppEvents.PlanChangedAsync` synchronizuje wszystkie widoki
- `Label` i `Name` s¹ obliczane dynamicznie na podstawie `Type`

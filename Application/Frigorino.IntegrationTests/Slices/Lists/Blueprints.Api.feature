Feature: Category Blueprint Sorting API

  Background:
    Given I am logged in with an active household

  Scenario: Applying a blueprint reorders unchecked items by aisle
    Given there is a list named "Weekly Groceries"
    When I POST an item with text "Spülmittel" to "Weekly Groceries" via the API
    And I POST an item with text "Milch" to "Weekly Groceries" via the API
    Then the product "spülmittel" is categorized as "HouseholdAndCleaning"
    And the product "milch" is categorized as "DairyAndEggs"
    When I create a blueprint named "My Store" ordered "DairyAndEggs, HouseholdAndCleaning" via the API
    And I apply blueprint "My Store" to "Weekly Groceries" via the API
    Then the unchecked items of "Weekly Groceries" are ordered "milch, spülmittel"

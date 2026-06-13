Feature: Re-order an inventory item back to a shopping list (SPA)

  Background:
    Given I am logged in with an active household

  Scenario: Adding an inventory item to the only list creates a list item and leaves the inventory untouched
    Given there is a list named "Weekly Groceries" with item "Bread"
    And there is an inventory named "Fridge" with item "Milk" and quantity "2"
    When I open the inventory "Fridge"
    And I open the inventory item menu for "Milk"
    And I click add to list from the inventory item menu
    And I confirm the re-order
    Then the list "Weekly Groceries" contains an item "Milk"
    And the list "Weekly Groceries" item "Milk" carries quantity "2" "Piece"
    And the inventory "Fridge" contains an item "Milk"

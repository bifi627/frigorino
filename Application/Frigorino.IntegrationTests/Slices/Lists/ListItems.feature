Feature: List Items

  Background:
    Given I am logged in with an active household

  Scenario: User adds an item to a list
    Given there is a list named "Weekly Groceries"
    When I open the list "Weekly Groceries"
    And I add item "Milk" to the list
    Then "Milk" appears in the list

  Scenario: User adds multiple items and they appear in entry order
    Given there is a list named "Weekly Groceries"
    When I open the list "Weekly Groceries"
    And I add item "Milk" to the list
    And I add item "Bread" to the list
    And I add item "Eggs" to the list
    Then the unchecked items appear in order: "Milk, Bread, Eggs"

  Scenario: User adds a list item with a quantity via the panel
    Given there is a list named "Weekly Groceries"
    When I open the list "Weekly Groceries"
    And I type "Milk" in the composer
    And I open the "quantity" composer panel
    And I set the quantity to "3"
    And I submit the composer
    Then "Milk" appears in the list
    And the list item "Milk" shows quantity "3"

  Scenario: The quantity chip stays visible while its panel is open
    Given there is a list named "Weekly Groceries"
    When I open the list "Weekly Groceries"
    And I type "Milk" in the composer
    And I open the "quantity" composer panel
    And I set the quantity to "3"
    Then the "quantity" composer chip is visible

  Scenario: User updates a list item quantity via the panel in edit mode
    Given there is a list named "Weekly Groceries" with item "Milk"
    When I open the list "Weekly Groceries"
    And I open the item menu for "Milk"
    And I start editing the item
    And I open the "quantity" composer panel
    And I set the quantity to "5"
    And I save the composer edit
    Then the list item "Milk" shows quantity "5"

  Scenario: User clears a list item quantity in edit mode
    Given there is a list named "Weekly Groceries" with item "Milk" and quantity "5"
    When I open the list "Weekly Groceries"
    And I open the item menu for "Milk"
    And I start editing the item
    And I open the "quantity" composer panel
    And I clear the quantity
    And I save the composer edit
    Then the list item "Milk" shows no quantity

  Scenario: User checks off a list item
    Given there is a list named "Weekly Groceries" with item "Milk"
    When I open the list "Weekly Groceries"
    And I toggle "Milk" as done
    Then "Milk" is shown as checked

  Scenario: User removes an item from the list via the row menu
    Given there is a list named "Weekly Groceries" with item "Milk"
    When I open the list "Weekly Groceries"
    And I open the item menu for "Milk"
    And I click delete from the item menu
    Then "Milk" no longer appears in the list

  Scenario: User reorders unchecked items by dragging
    Given there is a list named "Weekly Groceries" with item "Milk"
    And the list "Weekly Groceries" also has item "Bread"
    And the list "Weekly Groceries" also has item "Eggs"
    When I open the list "Weekly Groceries"
    And I enable drag handles
    And I drag "Eggs" above "Milk"
    Then the unchecked items appear in order: "Eggs, Milk, Bread"

  Scenario: Toggling an item back to unchecked moves it below other unchecked items
    Given there is a list named "Weekly Groceries" with item "Milk"
    And the list "Weekly Groceries" also has item "Bread"
    When I open the list "Weekly Groceries"
    And I toggle "Milk" as done
    And I toggle "Milk" as done
    Then the unchecked items appear in order: "Bread, Milk"

  Scenario: Undo restores a deleted list item via the toast
    Given there is a list named "Weekly Groceries" with item "Milk"
    When I open the list "Weekly Groceries"
    And I open the item menu for "Milk"
    And I click delete from the item menu
    Then "Milk" no longer appears in the list
    When I click undo in the delete toast
    Then "Milk" appears in the list

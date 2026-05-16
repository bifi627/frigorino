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

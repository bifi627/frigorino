Feature: List Item Search

  Background:
    Given I am logged in with an active household

  Scenario: Searching the list hides non-matching items
    Given there is a list named "Groceries" with item "Milk"
    And the list "Groceries" also has item "Bread"
    When I open the list "Groceries"
    And I open the list search
    And I search the list for "Mil"
    Then "Milk" appears in the list
    And "Bread" no longer appears in the list

  Scenario: Searching matches an item by its comment
    Given there is a list named "Groceries" with item "Bread"
    And the list "Groceries" also has item "Milk" with comment "organic"
    When I open the list "Groceries"
    And I open the list search
    And I search the list for "organic"
    Then "Milk" appears in the list
    And "Bread" no longer appears in the list

  Scenario: Searching disables drag handles, clearing restores them
    Given there is a list named "Groceries" with item "Milk"
    When I open the list "Groceries"
    And I enable drag handles
    Then the list item "Milk" shows a drag handle
    When I open the list search
    And I search the list for "Milk"
    Then the list item "Milk" shows no drag handle
    When I clear the list search
    Then the list item "Milk" shows a drag handle

  Scenario: A non-matching list search shows the no-results message
    Given there is a list named "Groceries" with item "Milk"
    When I open the list "Groceries"
    And I open the list search
    And I search the list for "zzz"
    Then the list search shows no results

Feature: List Items API

  Background:
    Given I am logged in with an active household

  Scenario: Creating an item with empty text returns a validation error
    Given there is a list named "Weekly Groceries"
    When I POST an item with empty text to "Weekly Groceries" via the API
    Then the API response status is 400
    And the API response has a validation error for "Text"

  Scenario: Non-member cannot read a list's items
    Given I am logged in as "alice"
    And an existing household "Other" owned by "bob" that I am not a member of
    And "bob" has created a list named "BobsList"
    When I GET the items of "BobsList" via the API
    Then the API response status is 404

  Scenario: Deleting an item via the API removes it from the list
    Given there is a list named "Weekly Groceries" with item "Milk"
    When I DELETE the item "Milk" in "Weekly Groceries" via the API
    Then the API response status is 204
    And the API response when getting items of "Weekly Groceries" omits "Milk"

  Scenario: Compacting items preserves the existing order while renumbering
    Given there is a list named "Weekly Groceries" with item "Milk"
    And the list "Weekly Groceries" also has item "Bread"
    And the list "Weekly Groceries" also has item "Eggs"
    When I POST compact for "Weekly Groceries" via the API
    Then the API response status is 204
    And the API items of "Weekly Groceries" appear in order: "Milk, Bread, Eggs"

  Scenario: Reordering an item to the top of section via the API
    Given there is a list named "Weekly Groceries" with item "Milk"
    And the list "Weekly Groceries" also has item "Bread"
    And the list "Weekly Groceries" also has item "Eggs"
    When I PATCH "Eggs" to the top of "Weekly Groceries" via the API
    Then the API response status is 200
    And the API items of "Weekly Groceries" appear in order: "Eggs, Milk, Bread"

  Scenario: Reordering an item after another via the API
    Given there is a list named "Weekly Groceries" with item "Milk"
    And the list "Weekly Groceries" also has item "Bread"
    And the list "Weekly Groceries" also has item "Eggs"
    When I PATCH "Bread" after "Eggs" in "Weekly Groceries" via the API
    Then the API response status is 200
    And the API items of "Weekly Groceries" appear in order: "Milk, Eggs, Bread"

  Scenario: Toggling an item back to unchecked places it below other unchecked items
    Given there is a list named "Weekly Groceries" with item "Milk"
    And the list "Weekly Groceries" also has item "Bread"
    When I PATCH toggle on "Milk" in "Weekly Groceries" via the API
    And I PATCH toggle on "Milk" in "Weekly Groceries" via the API
    Then the API items of "Weekly Groceries" appear in order: "Bread, Milk"

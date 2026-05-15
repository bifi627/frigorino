Feature: Shopping Lists

  Background:
    Given I am logged in with an active household

  Scenario: User creates a shopping list
    When I navigate to "/lists/create"
    And I fill in the list name "Weekly Groceries"
    And I submit the list form
    Then I am on the list view page

  Scenario: Created list appears on lists overview
    Given there is a list named "Weekly Groceries"
    When I navigate to "/lists"
    Then "Weekly Groceries" appears in the list overview

  Scenario: User adds an item to a list
    Given there is a list named "Weekly Groceries"
    When I open the list "Weekly Groceries"
    And I add item "Milk" to the list
    Then "Milk" appears in the list

  Scenario: User checks off a list item
    Given there is a list named "Weekly Groceries" with item "Milk"
    When I open the list "Weekly Groceries"
    And I toggle "Milk" as done
    Then "Milk" is shown as checked

  Scenario: User deletes a list
    Given there is a list named "Old List"
    When I navigate to "/lists"
    And I delete the list "Old List"
    Then "Old List" no longer appears in the list overview

  Scenario: User renames a list
    Given there is a list named "Old Name"
    When I open the list edit page for "Old Name"
    And I fill in the list name "New Name"
    And I save the list
    And I navigate to "/lists"
    Then "New Name" appears in the list overview

  Scenario: Creating a list with an empty name returns a validation error
    When I POST a list with an empty name via the API
    Then the API response status is 400
    And the API response has a validation error for "Name"

  Scenario: Non-member cannot read a household's lists
    Given I am logged in as "alice"
    And an existing household "Other" owned by "bob" that I am not a member of
    When I GET the lists of that household via the API
    Then the API response status is 404

  Scenario: Non-creator Member cannot delete a list
    Given I am logged in as "alice"
    And an existing household "Family" owned by "bob" with me as a "member"
    And "bob" has created a list named "BobsList"
    When I DELETE the list "BobsList" via the API
    Then the API response status is 403

  Scenario: Non-creator Admin can delete a list
    Given I am logged in as "alice"
    And an existing household "Family" owned by "bob" with me as a "admin"
    And "bob" has created a list named "BobsList"
    When I DELETE the list "BobsList" via the API
    Then the API response status is 204

Feature: Resource revision tokens

  Background:
    Given I am logged in with an active household

  Scenario: A no-op read returns the same list revision token
    Given there is a list named "Weekly Groceries" with item "Milk"
    When I capture the revision of list "Weekly Groceries" via the API
    And I capture the revision of list "Weekly Groceries" via the API
    Then the two captured revisions are equal

  Scenario: Adding an item changes the list revision token
    Given there is a list named "Weekly Groceries" with item "Milk"
    When I capture the revision of list "Weekly Groceries" via the API
    And I POST an item "Bread" with comment "" to "Weekly Groceries" via the API
    And I capture the revision of list "Weekly Groceries" via the API
    Then the two captured revisions differ

  Scenario: Deleting an item changes the list revision token
    Given there is a list named "Weekly Groceries" with item "Milk"
    When I capture the revision of list "Weekly Groceries" via the API
    And I DELETE the item "Milk" in "Weekly Groceries" via the API
    And I capture the revision of list "Weekly Groceries" via the API
    Then the two captured revisions differ

  Scenario: Renaming the list changes the list revision token
    Given there is a list named "Weekly Groceries" with item "Milk"
    When I capture the revision of list "Weekly Groceries" via the API
    And I rename the list "Weekly Groceries" to "Groceries" via the database
    And I capture the revision of list "Weekly Groceries" via the API
    Then the two captured revisions differ

  Scenario: Non-member cannot read a list revision
    Given I am logged in as "alice"
    And an existing household "Other" owned by "bob" that I am not a member of
    And "bob" has created a list named "BobsList"
    When I GET the revision of list "BobsList" via the API
    Then the API response status is 404

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

  Scenario: A no-op read returns the same inventory revision token
    Given an inventory "Fridge" has an item "Cheese" with no expiry
    When I capture the revision of inventory "Fridge" via the API
    And I capture the revision of inventory "Fridge" via the API
    Then the two captured revisions are equal

  Scenario: Adding an item changes the inventory revision token
    Given an inventory "Fridge" has an item "Cheese" with no expiry
    When I capture the revision of inventory "Fridge" via the API
    And an inventory "Fridge" has an item "Butter" with no expiry
    And I capture the revision of inventory "Fridge" via the API
    Then the two captured revisions differ

  Scenario: Editing an item's text changes the inventory revision token
    Given an inventory "Fridge" has an item "Cheese" with no expiry
    When I capture the revision of inventory "Fridge" via the API
    And I edit the text of item "Cheese" in inventory "Fridge" to "Gouda" via the database
    And I capture the revision of inventory "Fridge" via the API
    Then the two captured revisions differ

  Scenario: Deleting an inventory item changes the inventory revision token
    Given there is an inventory named "Fridge" with item "Cheese"
    When I capture the revision of inventory "Fridge" via the API
    And I DELETE the inventory item "Cheese" in "Fridge" via the API
    And I capture the revision of inventory "Fridge" via the API
    Then the two captured revisions differ

  Scenario: Non-member cannot read an inventory revision
    Given I am logged in as "alice"
    And an existing household "Other" owned by "bob" that I am not a member of
    And "bob" has created an inventory named "BobsFridge"
    When I GET the revision of inventory "BobsFridge" via the API
    Then the API response status is 404

  Scenario: Adding a perishable item changes the calendar revision token
    Given an inventory "Fridge" has an item "Yogurt" expiring in 3 days
    When I capture the expiry-calendar revision via the API
    And an inventory "Fridge" has an item "Milk" expiring in 5 days
    And I capture the expiry-calendar revision via the API
    Then the two captured revisions differ

  Scenario: Editing a non-perishable item does not change the calendar revision token
    Given an inventory "Fridge" has an item "Yogurt" expiring in 3 days
    And an inventory "Fridge" has an item "Salt" with no expiry
    When I capture the expiry-calendar revision via the API
    And I edit the text of item "Salt" in inventory "Fridge" to "Sea Salt" via the database
    And I capture the expiry-calendar revision via the API
    Then the two captured revisions are equal

  Scenario: Non-member cannot read the calendar revision
    Given I am logged in as "alice"
    And an existing household "Other" owned by "bob" that I am not a member of
    When I GET the expiry-calendar revision via the API
    Then the API response status is 404

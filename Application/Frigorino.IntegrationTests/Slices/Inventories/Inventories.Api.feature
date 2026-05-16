Feature: Inventories API

  Background:
    Given I am logged in with an active household

  Scenario: Creating an inventory with an empty name returns a validation error
    When I POST an inventory with an empty name via the API
    Then the API response status is 400
    And the API response has a validation error for "Name"

  Scenario: Non-member cannot read a household's inventories
    Given I am logged in as "alice"
    And an existing household "Other" owned by "bob" that I am not a member of
    When I GET the inventories of that household via the API
    Then the API response status is 404

  Scenario: Non-creator Member cannot delete an inventory
    Given I am logged in as "alice"
    And an existing household "Family" owned by "bob" with me as a "member"
    And "bob" has created an inventory named "BobsInventory"
    When I DELETE the inventory "BobsInventory" via the API
    Then the API response status is 403

  Scenario: Non-creator Admin can delete an inventory
    Given I am logged in as "alice"
    And an existing household "Family" owned by "bob" with me as a "admin"
    And "bob" has created an inventory named "BobsInventory"
    When I DELETE the inventory "BobsInventory" via the API
    Then the API response status is 204

Feature: Expiry Calendar API

  Background:
    Given I am logged in with an active household

  Scenario: Calendar returns items with an expiry across all inventories
    Given an inventory "Fridge" has an item "Milk" expiring in 2 days
    And an inventory "Pantry" has an item "Rice" expiring in 40 days
    And an inventory "Fridge" has an item "Salt" with no expiry
    When I GET the expiry calendar via the API
    Then the API response status is 200
    And the API expiry calendar contains "Milk"
    And the API expiry calendar contains "Rice"
    And the API expiry calendar does not contain "Salt"

  Scenario: Non-member cannot read the expiry calendar
    Given I am logged in as "alice"
    And an existing household "Other" owned by "bob" that I am not a member of
    When I GET the expiry calendar via the API
    Then the API response status is 404

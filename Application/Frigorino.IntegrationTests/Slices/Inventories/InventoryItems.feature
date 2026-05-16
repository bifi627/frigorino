Feature: Inventory Items

  Background:
    Given I am logged in with an active household

  Scenario: User adds an item to an inventory
    Given there is an inventory named "Pantry"
    When I open the inventory "Pantry"
    And I add item "Flour" to the inventory
    Then "Flour" appears in the inventory

  Scenario: User removes an inventory item via the row menu
    Given there is an inventory named "Pantry" with item "Flour"
    When I open the inventory "Pantry"
    And I open the inventory item menu for "Flour"
    And I click delete from the inventory item menu
    Then "Flour" no longer appears in the inventory

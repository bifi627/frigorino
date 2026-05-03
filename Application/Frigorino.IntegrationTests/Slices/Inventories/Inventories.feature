Feature: Inventories

  Background:
    Given I am logged in with an active household

  Scenario: User creates an inventory
    When I navigate to "/inventories/create"
    And I fill in the inventory name "Pantry"
    And I submit the inventory form
    Then I am on the inventory view page

  Scenario: Created inventory appears on inventories overview
    Given there is an inventory named "Pantry"
    When I navigate to "/inventories"
    Then "Pantry" appears in the inventory overview

  Scenario: User adds an item to an inventory
    Given there is an inventory named "Pantry"
    When I open the inventory "Pantry"
    And I add item "Flour" to the inventory
    Then "Flour" appears in the inventory

  Scenario: User deletes an inventory
    Given there is an inventory named "Old Inventory"
    When I navigate to "/inventories"
    And I delete the inventory "Old Inventory"
    Then "Old Inventory" no longer appears in the inventory overview

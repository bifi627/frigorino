Feature: Inventories

  Background:
    Given I am logged in with an active household

  Scenario: User creates an inventory
    When I navigate to "/inventories/create"
    And I fill in the inventory name "Pantry"
    And I submit the inventory form
    Then I am on the inventory view page

  Scenario: User deletes an inventory
    Given there is an inventory named "Old Inventory"
    When I navigate to "/inventories"
    And I delete the inventory "Old Inventory"
    Then "Old Inventory" no longer appears in the inventory overview

  Scenario: User renames an inventory
    Given there is an inventory named "Old Name"
    When I open the inventory edit page for "Old Name"
    And I fill in the inventory name "New Name"
    And I save the inventory
    And I navigate to "/inventories"
    Then "New Name" appears in the inventory overview

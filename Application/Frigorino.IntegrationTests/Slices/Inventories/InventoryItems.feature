Feature: Inventory Items

  Background:
    Given I am logged in with an active household

  Scenario: User adds an item to an inventory
    Given there is an inventory named "Pantry"
    When I open the inventory "Pantry"
    And I add item "Flour" to the inventory
    Then "Flour" appears in the inventory

  Scenario: User adds an inventory item with quantity and expiry via the panels
    Given there is an inventory named "Pantry"
    When I open the inventory "Pantry"
    And I type "Flour" in the composer
    And I open the "quantity" composer panel
    And I set the quantity to "2"
    And I open the "expiry" composer panel
    And I set the expiry date to "2030-12-31"
    And I submit the composer
    Then "Flour" appears in the inventory
    And the inventory item "Flour" shows quantity "2"
    And the inventory item "Flour" shows an expiry indicator

  Scenario: User clears an inventory item quantity in edit mode
    Given there is an inventory named "Pantry" with item "Flour" and quantity "5"
    When I open the inventory "Pantry"
    And I open the inventory item menu for "Flour"
    And I start editing the item
    And I open the "quantity" composer panel
    And I clear the quantity
    And I save the composer edit
    Then the inventory item "Flour" shows no quantity

  Scenario: User removes an inventory item via the row menu
    Given there is an inventory named "Pantry" with item "Flour"
    When I open the inventory "Pantry"
    And I open the inventory item menu for "Flour"
    And I click delete from the inventory item menu
    Then "Flour" no longer appears in the inventory

  Scenario: Undo restores a deleted inventory item via the toast
    Given there is an inventory named "Pantry" with item "Flour"
    When I open the inventory "Pantry"
    And I open the inventory item menu for "Flour"
    And I click delete from the inventory item menu
    Then "Flour" no longer appears in the inventory
    When I click undo in the delete toast
    Then "Flour" appears in the inventory

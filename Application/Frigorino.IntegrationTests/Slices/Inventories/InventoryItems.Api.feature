Feature: Inventory Items API

  Background:
    Given I am logged in with an active household

  Scenario: Creating an inventory item with empty text returns a validation error
    Given there is an inventory named "Pantry"
    When I POST an inventory item with empty text to "Pantry" via the API
    Then the API response status is 400
    And the API response has a validation error for "Text"

  Scenario: Non-member cannot read an inventory's items
    Given I am logged in as "alice"
    And an existing household "Other" owned by "bob" that I am not a member of
    And "bob" has created an inventory named "BobsInventory"
    When I GET the items of inventory "BobsInventory" via the API
    Then the API response status is 404

  Scenario: Deleting an inventory item via the API removes it from the inventory
    Given there is an inventory named "Pantry" with item "Flour"
    When I DELETE the inventory item "Flour" in "Pantry" via the API
    Then the API response status is 204
    And the API response when getting items of inventory "Pantry" omits "Flour"

  Scenario: Compacting inventory items preserves the existing order while renumbering
    Given there is an inventory named "Pantry" with item "Flour"
    And the inventory "Pantry" also has item "Sugar"
    And the inventory "Pantry" also has item "Salt"
    When I POST compact for inventory "Pantry" via the API
    Then the API response status is 204
    And the API items of inventory "Pantry" appear in order: "Flour, Sugar, Salt"

  Scenario: Reordering an inventory item to the top of section via the API
    Given there is an inventory named "Pantry" with item "Flour"
    And the inventory "Pantry" also has item "Sugar"
    And the inventory "Pantry" also has item "Salt"
    When I PATCH "Salt" to the top of inventory "Pantry" via the API
    Then the API response status is 200
    And the API items of inventory "Pantry" appear in order: "Salt, Flour, Sugar"

  Scenario: Reordering an inventory item after another via the API
    Given there is an inventory named "Pantry" with item "Flour"
    And the inventory "Pantry" also has item "Sugar"
    And the inventory "Pantry" also has item "Salt"
    When I PATCH "Sugar" after "Salt" in inventory "Pantry" via the API
    Then the API response status is 200
    And the API items of inventory "Pantry" appear in order: "Flour, Salt, Sugar"

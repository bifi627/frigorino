Feature: Inventory and Calendar Item Search

  Background:
    Given I am logged in with an active household

  Scenario: Searching the inventory hides non-matching items
    Given there is an inventory named "Pantry" with item "Flour"
    And the inventory "Pantry" also has item "Sugar"
    When I open the inventory "Pantry"
    And I open the inventory search
    And I search the inventory for "Flo"
    Then "Flour" appears in the inventory
    And "Sugar" no longer appears in the inventory

  Scenario: Searching disables drag handles, clearing restores them
    Given there is an inventory named "Pantry" with item "Flour"
    When I open the inventory "Pantry"
    Then the inventory item "Flour" shows a drag handle
    When I open the inventory search
    And I search the inventory for "Flour"
    Then the inventory item "Flour" shows no drag handle
    When I clear the inventory search
    Then the inventory item "Flour" shows a drag handle

  Scenario: A non-matching inventory search shows the no-results message
    Given there is an inventory named "Pantry" with item "Flour"
    When I open the inventory "Pantry"
    And I open the inventory search
    And I search the inventory for "zzz"
    Then the inventory search shows no results

  Scenario: Searching the calendar hides non-matching items
    Given an inventory "Fridge" has an item "Milk" expiring in 10 days
    And an inventory "Fridge" has an item "Rice" expiring in 10 days
    When I open the inventories overview
    And I open the expiry calendar from the header
    Then the calendar shows the item "Milk"
    When I open the calendar search
    And I search the calendar for "Mil"
    Then the calendar shows the item "Milk"
    And the calendar does not show the item "Rice"

Feature: Expiry Calendar

  Background:
    Given I am logged in with an active household

  Scenario: User opens the calendar from the inventories header and focuses an item
    # 10 days out keeps the bar wide enough to render the inline label + date stamp.
    Given an inventory "Fridge" has an item "Milk" expiring in 10 days
    When I open the inventories overview
    And I open the expiry calendar from the header
    Then the calendar shows the item "Milk"
    When I select the calendar item "Milk"
    Then the calendar item "Milk" is focused
    And the calendar action bar shows "Milk"

  Scenario: Tapping a short-span item also opens the action bar
    Given an inventory "Fridge" has an item "Yogurt" expiring in 2 days
    When I open the inventories overview
    And I open the expiry calendar from the header
    Then the calendar shows the item "Yogurt"
    When I select the calendar item "Yogurt"
    Then the calendar action bar shows "Yogurt"

  Scenario: User edits an item from the calendar and stays selected
    Given an inventory "Fridge" has an item "Milk" expiring in 10 days
    When I open the inventories overview
    And I open the expiry calendar from the header
    Then the calendar shows the item "Milk"
    When I select the calendar item "Milk"
    And I tap edit in the calendar action bar
    Then the calendar action bar is in edit mode
    When I change the item text to "Bread" and save
    Then the calendar shows the item "Bread"
    And the calendar action bar shows "Bread"
    And the calendar item "Bread" is focused

  Scenario: Filtering a level hides matching items and persists across reload
    Given an inventory "Fridge" has an item "Milk" expiring in 2 days
    And an inventory "Fridge" has an item "Rice" expiring in 40 days
    When I open the inventories overview
    And I open the expiry calendar from the header
    Then the calendar shows the item "Rice"
    When I turn off the "fresh" level filter
    Then the calendar does not show the item "Rice"
    When I reload the calendar page
    Then the calendar does not show the item "Rice"
    And the calendar shows the item "Milk"

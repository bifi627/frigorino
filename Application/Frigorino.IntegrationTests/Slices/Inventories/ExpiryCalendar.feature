Feature: Expiry Calendar

  Background:
    Given I am logged in with an active household

  Scenario: User opens the calendar from the inventories header and focuses an item
    # 10 days out keeps the bar wide enough to highlight (short-span bars open a details sheet instead).
    Given an inventory "Fridge" has an item "Milk" expiring in 10 days
    When I open the inventories overview
    And I open the expiry calendar from the header
    Then the calendar shows the item "Milk"
    When I select the calendar item "Milk"
    Then the calendar item "Milk" is focused

  Scenario: Tapping a short-span item opens the details sheet
    Given an inventory "Fridge" has an item "Yogurt" expiring in 2 days
    When I open the inventories overview
    And I open the expiry calendar from the header
    Then the calendar shows the item "Yogurt"
    When I select the calendar item "Yogurt"
    Then the item details sheet shows "Yogurt"

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

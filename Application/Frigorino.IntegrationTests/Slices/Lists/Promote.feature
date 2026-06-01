Feature: Promote checked items to inventory (SPA)

  Background:
    Given I am logged in with an active household

  Scenario: Checking off a classified perishable offers it for inventory and adds it
    Given there is a list named "Weekly Groceries" with item "Milk"
    And there is an inventory named "Fridge"
    And the product "milk" is in the catalog
    When I open the list "Weekly Groceries"
    And I toggle "Milk" as done
    Then the promote bar shows 1 item
    When I open the promote review sheet
    And I add the selected promote items
    Then the inventory "Fridge" contains an item "Milk"
    And the promote bar is not visible

  Scenario: Omitting an item removes it from the batch without adding it
    Given there is a list named "Weekly Groceries" with item "Milk"
    And there is an inventory named "Fridge"
    And the product "milk" is in the catalog
    When I open the list "Weekly Groceries"
    And I toggle "Milk" as done
    And I open the promote review sheet
    And I omit "Milk" from the promote sheet
    Then the promote bar is not visible

Feature: Copy recipe to shopping list (UI)

  Background:
    Given I am logged in with an active household

  Scenario: Copying a recipe's ingredients to a list from the recipe view
    Given there is a list named "Groceries"
    And there is a recipe named "Pancakes"
    And the recipe "Pancakes" has an item "Flour" with quantity 250 unit 0
    And the recipe "Pancakes" has an item "Milk" with quantity 300 unit 2
    When I open the recipe "Pancakes"
    And I open the copy-to-list sheet
    And I confirm the copy
    Then the list "Groceries" contains an item "Flour" with quantity 250 unit 0
    And the list "Groceries" contains an item "Milk" with quantity 300 unit 2

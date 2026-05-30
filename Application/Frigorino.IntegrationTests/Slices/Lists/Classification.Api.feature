Feature: Product Classification API

  Background:
    Given I am logged in with an active household

  Scenario: Adding a perishable list item classifies the product
    Given there is a list named "Weekly Groceries"
    When I POST an item with text "Milk" to "Weekly Groceries" via the API
    Then the product catalog eventually contains "milk" with AI-recommended shelf life 7

  Scenario: Adding a non-perishable list item classifies it as non-perishable
    Given there is a list named "Weekly Groceries"
    When I POST an item with text "Salt" to "Weekly Groceries" via the API
    Then the product catalog eventually contains "salt" as non-perishable

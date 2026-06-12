Feature: Product Classification API

  Background:
    Given I am logged in with an active household

  Scenario: Adding a perishable grocery classifies category and shelf life
    Given there is a list named "Weekly Groceries"
    When I POST an item with text "Milk" to "Weekly Groceries" via the API
    Then the product catalog eventually contains "milk" with AI-recommended shelf life 7
    And the product "milk" is categorized as "DairyAndEggs"

  Scenario: Adding a non-perishable grocery classifies it as non-perishable food
    Given there is a list named "Weekly Groceries"
    When I POST an item with text "Salt" to "Weekly Groceries" via the API
    Then the product catalog eventually contains "salt" as non-perishable
    And the product "salt" is categorized as "Pantry"

  Scenario: Adding a non-product task is categorized as Other
    Given there is a list named "Weekly Groceries"
    When I POST an item with text "Call Dentist" to "Weekly Groceries" via the API
    Then the product "call dentist" is categorized as "Other"

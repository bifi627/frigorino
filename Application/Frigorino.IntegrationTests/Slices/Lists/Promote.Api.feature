Feature: Promote suggestion on toggle (API)

  Background:
    Given I am logged in with an active household

  Scenario: Toggling a classified perishable item done returns a promote suggestion
    Given there is a list named "Weekly Groceries" with item "Milk"
    And the product "milk" is in the catalog
    When I toggle item "Milk" in list "Weekly Groceries" via the API
    Then the toggle response has a promote suggestion with handling "AiRecommendsShelfLife"
    And the promote suggestion has a non-null suggested expiry

  Scenario: Toggling a non-perishable item done returns no promote suggestion
    Given there is a list named "Weekly Groceries" with item "Sugar"
    And the product "sugar" is in the catalog
    When I toggle item "Sugar" in list "Weekly Groceries" via the API
    Then the product catalog eventually contains "sugar" as non-perishable
    And the toggle response has no promote suggestion

  Scenario: Toggling an item back to unchecked returns no promote suggestion
    Given there is a list named "Weekly Groceries" with item "Milk"
    And the product "milk" is in the catalog
    When I toggle item "Milk" in list "Weekly Groceries" via the API
    And I toggle item "Milk" in list "Weekly Groceries" via the API
    Then the toggle response has no promote suggestion

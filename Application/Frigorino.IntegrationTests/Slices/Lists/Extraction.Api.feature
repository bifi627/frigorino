Feature: Inline Quantity Extraction API

  Background:
    Given I am logged in with an active household

  Scenario: Adding "20 apples" extracts the count and renames the item
    Given there is a list named "Weekly Groceries"
    When I POST an item with text "20 apples" to "Weekly Groceries" via the API
    Then the list item eventually has text "apples" with quantity 20 unit 4

  Scenario: Adding "1l milk" extracts the volume and chains classification
    Given there is a list named "Weekly Groceries"
    When I POST an item with text "1l milk" to "Weekly Groceries" via the API
    Then the list item eventually has text "milk" with quantity 1 unit 3
    And the product "milk" is categorized as "DairyAndEggs"

  Scenario: A non-digit task is not extracted but is still classified
    Given there is a list named "Weekly Groceries"
    When I POST an item with text "Call Dentist" to "Weekly Groceries" via the API
    Then the product "call dentist" is categorized as "Other"

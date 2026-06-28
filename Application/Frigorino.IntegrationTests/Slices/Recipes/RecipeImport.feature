Feature: Recipe import UI

  Background:
    Given I am logged in with an active household

  Scenario: Importing a URL lands on the edit page
    Given I am on the recipes page
    When I open the import dialog
    And I submit the import URL "https://example.com/pancakes"
    Then I am taken to the recipe edit page

  Scenario: A page with no recipe shows an inline error
    Given I am on the recipes page
    When I open the import dialog
    And I submit the import URL "https://example.com/norecipe"
    Then the import dialog shows an error

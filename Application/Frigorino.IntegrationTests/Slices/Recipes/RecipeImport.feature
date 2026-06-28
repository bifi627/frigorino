Feature: Recipe import UI

  Background:
    Given I am logged in with an active household

  Scenario: Importing a URL from the create page lands on the edit page
    When I navigate to "/recipes/create"
    And I submit the import URL "https://example.com/pancakes"
    Then I am taken to the recipe edit page

  Scenario: A page with no recipe shows an inline error
    When I navigate to "/recipes/create"
    And I submit the import URL "https://example.com/norecipe"
    Then the import shows an error

  Scenario: A typed name takes precedence over the parsed recipe name
    When I navigate to "/recipes/create"
    And I fill in the recipe name "My Own Title"
    And I submit the import URL "https://example.com/pancakes"
    Then I am taken to the recipe edit page
    And the recipe name field shows "My Own Title"

  Scenario: An invalid URL keeps the import button disabled
    When I navigate to "/recipes/create"
    And I enter the import URL "not a url"
    Then the import submit is disabled

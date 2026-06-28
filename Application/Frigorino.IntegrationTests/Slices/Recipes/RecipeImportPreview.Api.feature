Feature: Recipe import preview API

  Background:
    Given I am logged in with an active household

  Scenario: Previewing a URL returns the recipe name
    When I preview the recipe URL "https://example.com/pancakes" via the API
    Then the API response status is 200
    And the preview response name is "Imported Pancakes"

  Scenario: Previewing a page with no recipe returns 422 with a no_recipe_found code
    When I preview the recipe URL "https://example.com/norecipe" via the API
    Then the API response status is 422
    And the API response has the import error code "no_recipe_found"

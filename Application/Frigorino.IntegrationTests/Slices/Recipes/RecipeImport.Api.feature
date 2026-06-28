Feature: Recipe import API

  Background:
    Given I am logged in with an active household

  Scenario: Importing a URL creates a recipe with ingredients and a source link
    When I import the recipe URL "https://example.com/pancakes" via the API
    Then the API response status is 201
    And the imported recipe has 2 ingredients
    And the imported recipe has 1 source link

  Scenario: Importing the created recipe extracts quantities without classifying products
    When I import the recipe URL "https://example.com/pancakes" via the API
    Then the API response status is 201
    And the imported recipe item eventually has text "apples" with quantity 20
    And the Products table is empty

  Scenario: Importing a page with no recipe returns 422 with a no_recipe_found code
    When I import the recipe URL "https://example.com/norecipe" via the API
    Then the API response status is 422
    And the API response has the import error code "no_recipe_found"

  Scenario: Importing an invalid URL returns a validation error
    When I import the recipe URL "not-a-url" via the API
    Then the API response status is 400
    And the API response has a validation error for "Url"

Feature: Recipes

  Background:
    Given I am logged in with an active household

  Scenario: User creates a recipe
    When I navigate to "/recipes/create"
    And I fill in the recipe name "Pasta Carbonara"
    And I submit the recipe form
    Then I am on the recipe view page for "Pasta Carbonara"

  Scenario: User adds an ingredient to a recipe
    Given there is a recipe named "Pasta Carbonara"
    When I open the recipe "Pasta Carbonara"
    And I add ingredient "Eggs" to the recipe
    Then "Eggs" appears in the recipe items

  Scenario: User adds a quantity to a recipe ingredient via edit mode
    Given there is a recipe named "Pasta Carbonara"
    When I open the recipe "Pasta Carbonara"
    And I add ingredient "Eggs" to the recipe
    And I open the ingredient item menu for "Eggs"
    And I start editing the item
    And I open the "quantity" composer panel
    And I set the quantity to "3"
    And I save the recipe item edit
    Then the recipe item "Eggs" shows quantity "3"

  Scenario: User deletes a recipe from the recipe list
    Given there is a recipe named "Old Recipe"
    When I navigate to "/recipes"
    And I open the recipe card menu for "Old Recipe"
    And I confirm deleting the recipe "Old Recipe" from the card menu
    Then "Old Recipe" no longer appears in the recipe overview
